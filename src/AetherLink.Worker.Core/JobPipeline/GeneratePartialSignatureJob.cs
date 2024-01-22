using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AElf;
using AetherLink.Contracts.Consumer;
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
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.JobPipeline;

public class GeneratePartialSignatureJob : AsyncBackgroundJob<GeneratePartialSignatureJobArgs>, ITransientDependency
{
    private readonly IPeerManager _peerManager;
    private readonly IObjectMapper _objectMapper;
    private readonly OracleInfoOptions _infoOptions;
    private readonly IRequestProvider _requestProvider;
    private readonly IDataMessageProvider _dataMessageProvider;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ILogger<GeneratePartialSignatureJob> _logger;
    private readonly IOracleContractProvider _oracleContractProvider;

    public GeneratePartialSignatureJob(IPeerManager peerManager, ILogger<GeneratePartialSignatureJob> logger,
        IObjectMapper objectMapper, IBackgroundJobManager backgroundJobManager, IRequestProvider requestProvider,
        IOptionsSnapshot<OracleInfoOptions> chainInfoOptions, IOracleContractProvider oracleContractProvider,
        IDataMessageProvider dataMessageProvider)
    {
        _logger = logger;
        _peerManager = peerManager;
        _objectMapper = objectMapper;
        _requestProvider = requestProvider;
        _infoOptions = chainInfoOptions.Value;
        _dataMessageProvider = dataMessageProvider;
        _backgroundJobManager = backgroundJobManager;
        _oracleContractProvider = oracleContractProvider;
    }

    public override async Task ExecuteAsync(GeneratePartialSignatureJobArgs args)
    {
        var chainId = args.ChainId;
        var reqId = args.RequestId;
        var epoch = args.Epoch;
        var roundId = args.RoundId;
        var observations = args.Observations;
        var argId = IdGeneratorHelper.GenerateId(chainId, reqId, epoch, roundId);
        _logger.LogInformation("[step4] {name} Start to validate report", argId);

        try
        {
            // Check Request State
            var request = await _requestProvider.GetAsync(args);
            if (request == null || request.State is RequestState.RequestCanceled) return;

            // Check Data In Report
            var dataMessage = await _dataMessageProvider.GetAsync(args);
            if (dataMessage != null && observations[_peerManager.GetOwnIndex()] != dataMessage.Data)
            {
                _logger.LogWarning("[step4] {name} Check data fail", argId);
                return;
            }

            await ProcessReportValidateResultAsync(args,
                await GeneratedPartialSignatureAsync(chainId, request.TransactionId, reqId, observations, epoch));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Follower][step4] {name} Sign report failed", argId);
        }
    }

    private async Task<PartialSignatureDto> GeneratedPartialSignatureAsync(string chainId, string transactionId,
        string requestId, IEnumerable<long> observations, long epoch)
    {
        if (!_infoOptions.ChainConfig.TryGetValue(chainId, out var chainConfig)) return new PartialSignatureDto();

        var transmitData = await _oracleContractProvider.GenerateTransmitDataAsync(chainId, requestId,
            transactionId, epoch, new LongList { Data = { observations } }.ToByteString());

        var msg = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(transmitData.Report.ToByteArray()),
            HashHelper.ComputeFrom(transmitData.ReportContext.ToString())).ToByteArray();

        var multiSignature = new MultiSignature(ByteArrayHelper.HexStringToByteArray(chainConfig.SignerSecret), msg,
            chainConfig.DistPublicKey, chainConfig.PartialSignaturesThreshold);
        return multiSignature.GeneratePartialSignature();
    }

    private async Task ProcessReportValidateResultAsync(GeneratePartialSignatureJobArgs args,
        PartialSignatureDto partialSig)
    {
        var reqId = args.RequestId;
        var epoch = args.Epoch;
        var roundId = args.RoundId;

        if (_peerManager.IsLeader(epoch, roundId))
        {
            _logger.LogInformation("[step4][Leader] {reqId}-{epoch} Insert partialSign in queue", reqId, epoch);

            var procJob = _objectMapper.Map<GeneratePartialSignatureJobArgs, GenerateMultiSignatureJobArgs>(args);
            procJob.PartialSignature = partialSig;
            await _backgroundJobManager.EnqueueAsync(procJob, BackgroundJobPriority.High);
            return;
        }

        var reportSign = _objectMapper.Map<GeneratePartialSignatureJobArgs, CommitSignatureRequest>(args);
        reportSign.Signature = ByteString.CopyFrom(partialSig.Signature);
        reportSign.Index = partialSig.Index;

        _logger.LogInformation("[Step4][Follower] Send report signature to leader.");

        await _peerManager.CommitToLeaderAsync(p => p.CommitSignatureAsync(reportSign), epoch, roundId);
        // var context = new CancellationTokenSource(TimeSpan.FromSeconds(GrpcConstants.DefaultRequestTimeout));
        // await _peerManager.CommitToLeaderAsync(
        //     p => p.CommitSignatureAsync(reportSign, cancellationToken: context.Token), epoch, roundId);
        _logger.LogInformation("[step4][Follower] {reqId}-{epoch} Waiting for leader transmitted.", reqId, epoch);
    }
}