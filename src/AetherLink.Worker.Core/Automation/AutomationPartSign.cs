using System;
using System.Linq;
using System.Threading.Tasks;
using AetherLink.Contracts.Automation;
using AetherLink.Worker.Core.Automation.Args;
using AetherLink.Worker.Core.Automation.Providers;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.DataFeeds;
using AetherLink.Worker.Core.PeerManager;
using AetherLink.Worker.Core.Provider;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Automation;

public class AutomationPartSign : AsyncBackgroundJob<ReportSignatureRequestArgs>, ITransientDependency
{
    private readonly IJobProvider _jobProvider;
    private readonly IPeerManager _peerManager;
    private readonly IRetryProvider _retryProvider;
    private readonly IStorageProvider _storageProvider;
    private readonly IContractProvider _contractProvider;
    private readonly ISignatureProvider _signatureProvider;
    private readonly ILogger<GeneratePartialSignatureJob> _logger;
    private readonly IOracleContractProvider _oracleContractProvider;

    public AutomationPartSign(IPeerManager peerManager, ILogger<GeneratePartialSignatureJob> logger,
        IJobProvider jobProvider, IOracleContractProvider oracleContractProvider, ISignatureProvider signatureProvider,
        IRetryProvider retryProvider, IContractProvider contractProvider, IStorageProvider storageProvider)
    {
        _logger = logger;
        _jobProvider = jobProvider;
        _peerManager = peerManager;
        _retryProvider = retryProvider;
        _storageProvider = storageProvider;
        _contractProvider = contractProvider;
        _signatureProvider = signatureProvider;
        _oracleContractProvider = oracleContractProvider;
    }

    public override async Task ExecuteAsync(ReportSignatureRequestArgs args)
    {
        var chainId = args.Context.ChainId;
        var upkeepId = args.Context.RequestId;
        var epoch = args.Context.Epoch;

        _logger.LogDebug($"[Automation] Get leader {upkeepId} partial signature request.");
        try
        {
            var commitment = await _oracleContractProvider.GetRequestCommitmentAsync(chainId, upkeepId);
            switch (AutomationHelper.GetTriggerType(commitment))
            {
                case TriggerType.Cron:
                    if (!await IsJobNeedExecuteAsync(args)) return;
                    if (ByteString.CopyFrom(args.Payload) != AutomationHelper.GetUpkeepPerformData(commitment))
                    {
                        _logger.LogError("[Automation] Is different with leader check data.");
                        return;
                    }

                    break;
                case TriggerType.Log:
                    var logUpkeepInfoId = IdGeneratorHelper.GenerateUpkeepInfoId(chainId, upkeepId);
                    var logUpkeepInfo = await _storageProvider.GetAsync<LogUpkeepInfoDto>(logUpkeepInfoId);
                    if (logUpkeepInfo == null) return;
                    if (!await ValidateLeaderUpkeepTriggerAsync(args)) return;

                    break;
                default:
                    throw new NotImplementedException();
            }

            await CommitToLeaderAsync(args.Context, args.Payload);

            _logger.LogInformation(
                $"[Automation] Send {upkeepId}-{epoch} signature to leader, Waiting for transmitted.");
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[Automation] {upkeepId}-{epoch} Sign report failed");
        }
    }

    private async Task CommitToLeaderAsync(OCRContext context, byte[] payload)
    {
        var result = ByteString.CopyFrom(payload);
        var partialSig = await _signatureProvider.GeneratePartialSignAsync(context, result);

        await _peerManager.CommitToLeaderAsync(p => p.CommitPartialSignatureAsync(new CommitPartialSignatureRequest
        {
            Context = context,
            Signature = ByteString.CopyFrom(partialSig.Signature),
            Index = partialSig.Index,
            Payload = result
        }), context);
    }

    private async Task<bool> IsJobNeedExecuteAsync(ReportSignatureRequestArgs args)
    {
        var argRequestId = args.Context.RequestId;
        var argRoundId = args.Context.RoundId;
        var argEpoch = args.Context.Epoch;
        var job = await _jobProvider.GetAsync(args.Context);
        if (job == null)
        {
            _logger.LogInformation("[Automation] {reqId}-{epoch} is not exist yet.", argRequestId, argEpoch);
            await _retryProvider.RetryAsync(args.Context, args, backOff: true);
            return false;
        }

        var localEpoch = job.Epoch;
        var localRound = job.RoundId;

        if (argEpoch > localEpoch || (argEpoch == localEpoch && job.State is RequestState.RequestEnd))
        {
            _logger.LogInformation("[Automation] {reqId}-{epoch} is not ready yet.", argRequestId, argEpoch);
            await _retryProvider.RetryAsync(args.Context, args, delay: argEpoch - localEpoch);
            return false;
        }

        if (job.State is RequestState.RequestCanceled)
        {
            _logger.LogInformation("[Automation] {RequestId} is canceled.", argRequestId);
            return false;
        }

        var newRoundId = _peerManager.GetCurrentRoundId(job.RequestReceiveTime, job.RequestEndTimeoutWindow);
        if (argRoundId != newRoundId)
        {
            _logger.LogInformation("[Automation] {RequestId} round is not match, round:{RoundId}.", argRequestId,
                localRound);
            return false;
        }

        if (argEpoch == localEpoch) return true;

        _logger.LogInformation("[Automation] {RequestId} epoch is not match, epoch:{epoch}.", argRequestId, localEpoch);

        return false;
    }

    private async Task<bool> ValidateLeaderUpkeepTriggerAsync(ReportSignatureRequestArgs args)
    {
        var logUpkeepInfoId = IdGeneratorHelper.GenerateUpkeepInfoId(args.Context.ChainId, args.Context.RequestId);
        var logUpkeepInfo = await _storageProvider.GetAsync<LogUpkeepInfoDto>(logUpkeepInfoId);
        if (logUpkeepInfo == null)
        {
            _logger.LogWarning($"[Automation] Received a non-existent {logUpkeepInfoId} upkeep from leader.");
            return false;
        }

        // check ocr context 
        var checkData = LogTriggerCheckData.Parser.ParseFrom(args.Payload);
        var latestEpoch = await _oracleContractProvider.GetStartEpochAsync(checkData.ChainId, checkData.BlockHeight);
        if (args.Context.Epoch != latestEpoch)
        {
            _logger.LogError("[Automation] OCR context does not match.");
            return false;
        }

        // check transaction info
        var result = await _contractProvider.GetTxResultAsync(checkData.ChainId, checkData.TransactionId);
        if (result.BlockHash != checkData.BlockHash || result.BlockNumber != checkData.BlockHeight)
        {
            _logger.LogError("[Automation] Block information does not match.");
            return false;
        }

        // check event info
        if (!result.Logs.Any() || result.Logs.Length < checkData.Index) return false;
        var logEvent = result.Logs[checkData.Index];
        if (logEvent.Address != checkData.ContractAddress || logEvent.Name != checkData.EventName)
        {
            _logger.LogError("[Automation] Event information does not match.");
            return false;
        }

        if (logUpkeepInfo.TriggerChainId == checkData.ChainId &&
            logUpkeepInfo.TriggerEventName == checkData.EventName &&
            logUpkeepInfo.TriggerContractAddress == checkData.ContractAddress) return true;

        _logger.LogError("[Automation] Upkeep information does not match.");

        return false;
    }
}