using System;
using System.Threading.Tasks;
using AetherLink.Contracts.Automation;
using AetherLink.Worker.Core.Automation.Args;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline;
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
    private readonly ISignatureProvider _signatureProvider;
    private readonly ILogger<GeneratePartialSignatureJob> _logger;
    private readonly IOracleContractProvider _oracleContractProvider;

    public AutomationPartSign(IPeerManager peerManager, ILogger<GeneratePartialSignatureJob> logger,
        IJobProvider jobProvider, IOracleContractProvider oracleContractProvider, ISignatureProvider signatureProvider,
        IRetryProvider retryProvider)
    {
        _logger = logger;
        _jobProvider = jobProvider;
        _peerManager = peerManager;
        _retryProvider = retryProvider;
        _signatureProvider = signatureProvider;
        _oracleContractProvider = oracleContractProvider;
    }

    public override async Task ExecuteAsync(ReportSignatureRequestArgs req)
    {
        var chainId = req.Context.ChainId;
        var reqId = req.Context.RequestId;
        var epoch = req.Context.Epoch;
        var roundId = req.Context.RoundId;

        _logger.LogDebug($"[Automation] Get leader {reqId} partial signature request.");
        try
        {
            var job = await _jobProvider.GetAsync(req.Context);
            if (!await IsJobNeedExecuteAsync(req, job)) return;

            var commitment = await _oracleContractProvider.GetRequestCommitmentAsync(chainId, reqId);
            var originInput = RegisterUpkeepInput.Parser.ParseFrom(commitment.SpecificData);

            if (ByteString.CopyFrom(req.CheckData) != originInput.PerformData)
            {
                _logger.LogError("[Automation] Is different with leader check data.");
                return;
            }

            var partialSig = await _signatureProvider.GeneratePartialSignAsync(req.Context, originInput.PerformData);
            await _peerManager.CommitToLeaderAsync(p => p.CommitPartialSignatureAsync(new CommitPartialSignatureRequest
            {
                Context = req.Context,
                Signature = ByteString.CopyFrom(partialSig.Signature),
                Index = partialSig.Index
            }), epoch, roundId);

            _logger.LogInformation("[Automation] {reqId}-{epoch} Send signature to leader, Waiting for transmitted.",
                reqId, epoch);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Automation]{req} Sign report failed", reqId);
        }
    }

    private async Task<bool> IsJobNeedExecuteAsync(ReportSignatureRequestArgs args, JobDto job)
    {
        var argRequestId = args.Context.RequestId;
        var argRoundId = args.Context.RoundId;
        var argEpoch = args.Context.Epoch;

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

        if (localRound > argRoundId || argEpoch < localEpoch)
        {
            _logger.LogInformation("[Automation] {RequestId} is not match, epoch:{epoch} round:{Round}.", argRequestId,
                localEpoch, localRound);
            return false;
        }

        return true;
    }
}