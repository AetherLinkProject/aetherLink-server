using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AElf;
using AetherLink.Contracts.Consumer;
using AetherLink.Contracts.Oracle;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AetherLink.Multisignature;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Consts;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.PeerManager;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Scheduler;
using Oracle;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.JobPipeline;

public class LeaderPartialSigProcessJob : AsyncBackgroundJob<LeaderPartialSigProcessJobArgs>, ISingletonDependency
{
    private readonly IPeerManager _peerManager;
    private readonly OracleInfoOptions _options;
    private readonly IObjectMapper _objectMapper;
    private readonly IStateProvider _stateProvider;
    private readonly IContractProvider _contractProvider;
    private readonly ISchedulerService _schedulerService;
    private static readonly ReaderWriterLock Lock = new();
    private readonly IJobRequestProvider _jobRequestProvider;
    private readonly ILogger<LeaderPartialSigProcessJob> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IOracleContractProvider _oracleContractProvider;

    public LeaderPartialSigProcessJob(ILogger<LeaderPartialSigProcessJob> logger,
        IOptionsSnapshot<OracleInfoOptions> oracleChainInfoOptions, IJobRequestProvider jobRequestProvider,
        IPeerManager peerManager, IContractProvider contractProvider, ISchedulerService schedulerService,
        IStateProvider stateProvider, IOracleContractProvider oracleContractProvider,
        IBackgroundJobManager backgroundJobManager, IObjectMapper objectMapper)
    {
        _logger = logger;
        _peerManager = peerManager;
        _objectMapper = objectMapper;
        _stateProvider = stateProvider;
        _schedulerService = schedulerService;
        _contractProvider = contractProvider;
        _jobRequestProvider = jobRequestProvider;
        _options = oracleChainInfoOptions.Value;
        _backgroundJobManager = backgroundJobManager;
        _oracleContractProvider = oracleContractProvider;
    }

    public override async Task ExecuteAsync(LeaderPartialSigProcessJobArgs args)
    {
        var reqId = args.RequestId;
        var chainId = args.ChainId;
        var roundId = args.RoundId;
        var epoch = args.Epoch;
        var argsName = IdGeneratorHelper.GenerateId(chainId, reqId, epoch, roundId);
        _logger.LogInformation("[Step5][Leader] {name} Start.", argsName);

        try
        {
            var request = await _jobRequestProvider.GetJobRequestAsync(chainId, reqId, epoch);
            if (request == null || request.State == RequestState.RequestEnd)
            {
                _logger.LogWarning("[Step5][Leader] {name} Request is null.", argsName);
                return;
            }

            var observations = await _jobRequestProvider.GetReportAsync(chainId, reqId, epoch);
            if (observations.Count == 0)
            {
                _logger.LogWarning("[Step5][Leader] {name} Report is null.", argsName);
                return;
            }

            var commitment = await _oracleContractProvider.GetCommitmentAsync(chainId, request.TransactionId, reqId);
            var transmitData = new TransmitInput
            {
                Report = new Report
                {
                    Result = new LongList { Data = { observations } }.ToByteString(),
                    OnChainMetadata = commitment.ToByteString(),
                    Error = ByteString.Empty,
                    OffChainMetadata = ByteString.Empty
                }.ToByteString()
            };

            // get config fill ReportContext
            var latestConfigDigest = await _peerManager.GetLatestConfigDigestAsync(chainId);
            transmitData.ReportContext.Add(latestConfigDigest);

            // get latest round fill ReportContext
            transmitData.ReportContext.Add(HashHelper.ComputeFrom(await _peerManager.GetEpochAsync(chainId)));
            transmitData.ReportContext.Add(HashHelper.ComputeFrom(0));

            var msg = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(transmitData.Report.ToByteArray()),
                HashHelper.ComputeFrom(transmitData.ReportContext.ToString())).ToByteArray();

            if (!IsNeedSignature(args, msg))
            {
                _logger.LogWarning("[Step5][Leader] {name} No need sign.", argsName);
                return;
            }

            _logger.LogInformation("[Step5][Leader] {name} Signature success.", argsName);

            var multiSignature = _stateProvider.GetMultiSignature(GenerateMultiSignatureId(args));
            if (multiSignature == null)
            {
                _logger.LogWarning("[Step5][Leader] {name} MultiSignature not exist.", argsName);
                return;
            }

            multiSignature.TryGetSignatures(out var signature);
            transmitData.Signatures.AddRange(signature);

            var transactionId = await _contractProvider.SendTransmitAsync(chainId, transmitData);
            _logger.LogInformation("[step5][Leader] {name} Transmit transaction {transactionId}", argsName,
                transactionId);

            // cancel check report commit scheduler
            _schedulerService.CancelScheduler(request, SchedulerType.CheckReportCommitScheduler);

            request.State = RequestState.RequestTransmitted;
            await _jobRequestProvider.SetJobRequestAsync(request);

            var txResult = _objectMapper.Map<LeaderPartialSigProcessJobArgs, TransactionResult>(args);
            txResult.TransmitTransactionId = transactionId;
            await _peerManager.BroadcastRequestAsync(new StreamMessage
            {
                MessageType = MessageType.TransmittedResult,
                RequestId = reqId,
                Message = txResult.ToBytesValue().Value
            });

            var finishArgs = _objectMapper.Map<RequestDto, FinishedProcessJobArgs>(request);
            finishArgs.TransactionId = transactionId;
            await _backgroundJobManager.EnqueueAsync(finishArgs, delay: TimeSpan.FromSeconds(3));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Step5][Leader] {name} SendTransaction Failed.", argsName);
        }
    }

    private bool IsNeedSignature(LeaderPartialSigProcessJobArgs args, byte[] msg)
    {
        try
        {
            Lock.AcquireWriterLock(Timeout.Infinite);
            if (!_options.ChainConfig.TryGetValue(args.ChainId, out var chainConfig))
                return false;

            var multiSignId = GenerateMultiSignatureId(args);
            var multiSignature = _stateProvider.GetMultiSignature(multiSignId);
            if (multiSignature == null)
            {
                _logger.LogInformation("[step5] Init MultiSignature in epoch {epoch}", args.Epoch);
                multiSignature = new MultiSignature(ByteArrayHelper.HexStringToByteArray(chainConfig.SignerSecret), msg,
                    chainConfig.DistPublicKey, chainConfig.PartialSignaturesThreshold);
                _stateProvider.SetMultiSignature(multiSignId, multiSignature);
            }

            // Prevent duplicate submissions
            if (!multiSignature.ProcessPartialSignature(args.PartialSignature))
            {
                _logger.LogError("[step5] Epoch {epoch} Process Partial Index {index} Signature failed.", args.Epoch,
                    args.PartialSignature.Index);
                return false;
            }

            _logger.LogInformation("[step5] Epoch {epoch} Process Partial Index {index} Signature success.", args.Epoch,
                args.PartialSignature.Index);

            // multiSignatureSignedFlag = true when Enough PartialSignature and multiSignatureSignedFlag is not set
            var flag = _stateProvider.GetMultiSignatureSignedFlag(multiSignId);
            if (multiSignature.IsEnoughPartialSig() && !flag)
            {
                _stateProvider.SetMultiSignatureSignedFlag(multiSignId);
                return true;
            }

            return false;
        }
        finally
        {
            Lock.ReleaseWriterLock();
        }
    }

    private string GenerateMultiSignatureId(LeaderPartialSigProcessJobArgs args)
    {
        return IdGeneratorHelper.GenerateId(args.ChainId, args.RequestId, args.Epoch, args.RoundId);
    }
}