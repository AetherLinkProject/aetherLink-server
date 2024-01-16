using System;
using System.Collections.Generic;
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

public class FollowerReportProcessJob : AsyncBackgroundJob<FollowerReportProcessJobArgs>, ITransientDependency
{
    private readonly IPeerManager _peerManager;
    private readonly IObjectMapper _objectMapper;
    private readonly IRetryProvider _retryProvider;
    private readonly OracleInfoOptions _infoOptions;
    private readonly ISchedulerService _schedulerService;
    private readonly IJobRequestProvider _jobRequestProvider;
    private readonly ILogger<FollowerReportProcessJob> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IOracleContractProvider _oracleContractProvider;

    public FollowerReportProcessJob(IPeerManager peerManager, IJobRequestProvider jobRequestProvider,
        ILogger<FollowerReportProcessJob> logger, IBackgroundJobManager backgroundJobManager,
        IOptionsSnapshot<OracleInfoOptions> chainInfoOptions, ISchedulerService schedulerService,
        IOracleContractProvider oracleContractProvider, IObjectMapper objectMapper, IRetryProvider retryProvider)
    {
        _logger = logger;
        _peerManager = peerManager;
        _objectMapper = objectMapper;
        _retryProvider = retryProvider;
        _schedulerService = schedulerService;
        _infoOptions = chainInfoOptions.Value;
        _jobRequestProvider = jobRequestProvider;
        _backgroundJobManager = backgroundJobManager;
        _oracleContractProvider = oracleContractProvider;
    }

    public override async Task ExecuteAsync(FollowerReportProcessJobArgs args)
    {
        var reqId = args.RequestId;
        var chainId = args.ChainId;
        var roundId = args.RoundId;
        var observations = args.Observations;
        var epoch = args.Epoch;
        var argsName = IdGeneratorHelper.GenerateId(chainId, reqId, epoch, roundId);
        _logger.LogInformation("[step4] {name} Start to validate report, ", argsName);

        try
        {
            // Check Request State
            var request = await _jobRequestProvider.GetJobRequestAsync(chainId, reqId, epoch);
            if (request == null || request.State == RequestState.RequestEnd) return;

            // Check Data In Report
            var dataMessage = await _jobRequestProvider.GetDataMessageAsync(chainId, reqId, epoch);
            if (dataMessage == null)
            {
                _logger.LogInformation("[step4] {name} Data msg is null", argsName);
                await _retryProvider.RetryAsync(args);
                return;
            }

            var index = _peerManager.GetOwnIndex();
            if (index == dataMessage.Index && observations[index] == dataMessage.Data)
            {
                _logger.LogInformation("[step4] {name} Check data success.", argsName);
                var partialSig =
                    await GeneratedPartialSignatureAsync(chainId, request.TransactionId, reqId, observations);

                if (await _peerManager.IsLeaderAsync(chainId, roundId))
                {
                    _logger.LogInformation("[step4][Leader] {name} Insert partialSign in queue", argsName);

                    var procJob = _objectMapper.Map<FollowerReportProcessJobArgs, LeaderPartialSigProcessJobArgs>(args);
                    procJob.PartialSignature = partialSig;
                    await _backgroundJobManager.EnqueueAsync(procJob, BackgroundJobPriority.High);
                }
                else
                {
                    _schedulerService.CancelScheduler(request, SchedulerType.CheckReportReceiveScheduler);

                    var reportSign = _objectMapper.Map<FollowerReportProcessJobArgs, ReportSignature>(args);
                    reportSign.Signature = ByteString.CopyFrom(partialSig.Signature);
                    reportSign.Index = partialSig.Index;

                    _logger.LogInformation("[Step4][Follower] Send report signature to leader.");
                    await _peerManager.RequestLeaderAsync(new StreamMessage
                    {
                        MessageType = MessageType.RequestReportSignature,
                        RequestId = reqId,
                        Message = reportSign.ToBytesValue().Value
                    }, chainId, roundId);
                }

                request.ReportSignTime = args.ReportStartSignTime.ToDateTime();
                request.State = RequestState.ReportSigned;
                await _jobRequestProvider.SetJobRequestAsync(request);
                _logger.LogInformation("[step4][Follower] {name} Waiting for transmit", argsName);
                _schedulerService.StartScheduler(request, SchedulerType.CheckTransmitScheduler);
            }
            else
            {
                _logger.LogWarning("[step4] {name} Check data fail", argsName);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Follower][step4] {name} Sign report failed", argsName);
        }
    }

    private async Task<PartialSignatureDto> GeneratedPartialSignatureAsync(string chainId, string transactionId,
        string requestId,
        List<long> observations)
    {
        if (!_infoOptions.ChainConfig.TryGetValue(chainId, out var chainConfig)) return new PartialSignatureDto();

        var commitment = await _oracleContractProvider.GetCommitmentAsync(chainId, transactionId, requestId);
        var report = new Report
        {
            Result = new LongList
            {
                Data = { observations }
            }.ToByteString(),
            OnChainMetadata = commitment.ToByteString(),
            Error = ByteString.Empty,
            OffChainMetadata = ByteString.Empty
        }.ToByteString();

        var transmitData = new TransmitInput { Report = report };
        transmitData.ReportContext.Add(await _peerManager.GetLatestConfigDigestAsync(chainId));

        // get latest round fill ReportContext
        transmitData.ReportContext.Add(HashHelper.ComputeFrom(await _peerManager.GetEpochAsync(chainId)));
        transmitData.ReportContext.Add(HashHelper.ComputeFrom(0));

        var msg = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(transmitData.Report.ToByteArray()),
            HashHelper.ComputeFrom(transmitData.ReportContext.ToString())).ToByteArray();

        var multiSignature = new MultiSignature(ByteArrayHelper.HexStringToByteArray(chainConfig.SignerSecret), msg,
            chainConfig.DistPublicKey, chainConfig.PartialSignaturesThreshold);
        return multiSignature.GeneratePartialSignature();
    }
}