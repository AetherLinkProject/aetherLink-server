using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AElf;
using AetherLink.Contracts.Consumer;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AetherLink.Multisignature;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.PeerManager;
using AetherLink.Worker.Core.Provider;
using Newtonsoft.Json;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.JobPipeline;

public class GeneratePartialSignatureJob : AsyncBackgroundJob<GeneratePartialSignatureJobArgs>, ITransientDependency
{
    private readonly IJobProvider _jobProvider;
    private readonly IPeerManager _peerManager;
    private readonly IObjectMapper _objectMapper;
    private readonly OracleInfoOptions _infoOptions;
    private readonly IDataMessageProvider _dataMessageProvider;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ILogger<GeneratePartialSignatureJob> _logger;
    private readonly IOracleContractProvider _oracleContractProvider;

    public GeneratePartialSignatureJob(IPeerManager peerManager, ILogger<GeneratePartialSignatureJob> logger,
        IObjectMapper objectMapper, IBackgroundJobManager backgroundJobManager, IJobProvider jobProvider,
        IOptionsSnapshot<OracleInfoOptions> chainInfoOptions, IOracleContractProvider oracleContractProvider,
        IDataMessageProvider dataMessageProvider)
    {
        _logger = logger;
        _jobProvider = jobProvider;
        _peerManager = peerManager;
        _objectMapper = objectMapper;
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
        var argId = IdGeneratorHelper.GenerateId(chainId, reqId, epoch, roundId);
        _logger.LogInformation("[step4] {name} Start to validate report", argId);

        var observation = ByteString.FromBase64(args.Observations);
        try
        {
            // Check Request State
            var job = await _jobProvider.GetAsync(args);
            if (job == null || job.State is RequestState.RequestCanceled) return;
            var jobSpec = JsonConvert.DeserializeObject<DataFeedsDto>(job.JobSpec).DataFeedsJobSpec;

            if (jobSpec.Type == DataFeedsType.PlainDataFeeds)
            {
                var pendingConfirmData = Encoding.UTF8.GetString(observation.ToByteArray());
                var plainData = await _dataMessageProvider.GetPlainDataFeedsAsync(args);
                if (pendingConfirmData != plainData.NewData)
                {
                    _logger.LogError("[step4] Local data {ld} is inconsistent with the leader's data {pd}.",
                        plainData.NewData, pendingConfirmData);
                    return;
                }

                plainData.OldData = plainData.NewData;
                await _dataMessageProvider.SetAsync(plainData);
            }
            else
            {
                var observations = LongList.Parser.ParseFrom(observation).Data;
                _logger.LogDebug("[step4] {name} Leader report: {result}", argId, observations.JoinAsString(","));
                if (observations.Count < _peerManager.GetPeersCount())
                {
                    _logger.LogWarning("[step4] {name} observations {count} count not enough", argId,
                        observations.Count);
                    return;
                }

                // Check Data In Report
                var dataMessage = await _dataMessageProvider.GetAsync(args);
                if ((dataMessage != null && observations[_peerManager.GetOwnIndex()] != dataMessage.Data) ||
                    (dataMessage == null && observations[_peerManager.GetOwnIndex()] != 0))
                {
                    _logger.LogWarning("[step4] {name} Check data fail", argId);
                    return;
                }
            }

            await ProcessReportValidateResultAsync(args, await GeneratedPartialSignatureAsync(chainId,
                job.TransactionId, reqId, ByteString.FromBase64(args.Observations), epoch));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Follower][step4] {name} Sign report failed", argId);
        }
    }

    private async Task<PartialSignatureDto> GeneratedPartialSignatureAsync(string chainId, string transactionId,
        string requestId, ByteString observations, long epoch)
    {
        if (!_infoOptions.ChainConfig.TryGetValue(chainId, out var chainConfig))
        {
            throw new InvalidDataException($"Not support chain {chainId}.");
        }

        var transmitData = await _oracleContractProvider.GenerateTransmitDataAsync(chainId, requestId,
            transactionId, epoch, observations);

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
        await _peerManager.CommitToLeaderAsync(p => p.CommitSignatureAsync(reportSign), epoch,
            roundId);

        _logger.LogInformation("[step4][Follower] {reqId}-{epoch} Send signature to leader, Waiting for transmitted.",
            reqId, epoch);
    }
}