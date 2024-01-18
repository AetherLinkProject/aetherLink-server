using AElf;
using System;
using System.Threading;
using System.Threading.Tasks;
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

public class GenerateMultiSignatureJob : AsyncBackgroundJob<GenerateMultiSignatureJobArgs>, ISingletonDependency
{
    private readonly IPeerManager _peerManager;
    private readonly OracleInfoOptions _options;
    private readonly IObjectMapper _objectMapper;
    private readonly IStateProvider _stateProvider;
    private readonly IReportProvider _reportProvider;
    private readonly IRequestProvider _requestProvider;
    private readonly IContractProvider _contractProvider;
    private static readonly ReaderWriterLock Lock = new();
    private readonly ILogger<GenerateMultiSignatureJob> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IOracleContractProvider _oracleContractProvider;

    public GenerateMultiSignatureJob(ILogger<GenerateMultiSignatureJob> logger, IStateProvider stateProvider,
        IOptionsSnapshot<OracleInfoOptions> oracleChainInfoOptions, IContractProvider contractProvider,
        IOracleContractProvider oracleContractProvider, IObjectMapper objectMapper, IRequestProvider requestProvider,
        IReportProvider reportProvider, IPeerManager peerManager, IBackgroundJobManager backgroundJobManager)
    {
        _logger = logger;
        _peerManager = peerManager;
        _objectMapper = objectMapper;
        _stateProvider = stateProvider;
        _reportProvider = reportProvider;
        _requestProvider = requestProvider;
        _contractProvider = contractProvider;
        _options = oracleChainInfoOptions.Value;
        _backgroundJobManager = backgroundJobManager;
        _oracleContractProvider = oracleContractProvider;
    }

    public override async Task ExecuteAsync(GenerateMultiSignatureJobArgs args)
    {
        var reqId = args.RequestId;
        var chainId = args.ChainId;
        var roundId = args.RoundId;
        var epoch = args.Epoch;
        var argId = IdGeneratorHelper.GenerateId(chainId, reqId, epoch, roundId);

        try
        {
            var request = await _requestProvider.GetAsync(args);
            if (request == null || request.State == RequestState.RequestCanceled) return;

            var observations = await _reportProvider.GetAsync(args);
            if (observations == null)
            {
                _logger.LogWarning("[Step5][Leader] {name} Report is null.", argId);
                return;
            }

            var transmitData = await _oracleContractProvider.GenerateTransmitDataAsync(chainId, reqId,
                request.TransactionId, epoch, new LongList { Data = { observations.Observations } }.ToByteString());

            var msg = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(transmitData.Report.ToByteArray()),
                HashHelper.ComputeFrom(transmitData.ReportContext.ToString())).ToByteArray();

            if (!IsSignatureEnough(args, msg)) return;

            _logger.LogInformation("[Step5][Leader] {name} MultiSignature generate success.", argId);

            var multiSignature = _stateProvider.GetMultiSignature(GenerateMultiSignatureId(args));
            multiSignature.TryGetSignatures(out var signature);
            transmitData.Signatures.AddRange(signature);

            // send transmit transaction to oracle contract
            var transactionId = await _contractProvider.SendTransmitAsync(chainId, transmitData);
            _logger.LogInformation("[step5][Leader] {name} Transmit transaction {transactionId}", argId,
                transactionId);

            await ProcessTransactionResultAsync(args, transactionId, request);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Step5][Leader] {name} SendTransaction Failed.", argId);
        }
    }

    private bool IsSignatureEnough(GenerateMultiSignatureJobArgs args, byte[] msg)
    {
        try
        {
            if (!_options.ChainConfig.TryGetValue(args.ChainId, out var chainConfig)) return false;

            Lock.AcquireWriterLock(Timeout.Infinite);

            var id = GenerateMultiSignatureId(args);
            var sign = AddOrUpdateMultiSignature(chainConfig, id, msg, args.PartialSignature);
            if (!sign.IsEnoughPartialSig() || _stateProvider.GetMultiSignatureSignedFlag(id)) return false;

            _stateProvider.SetMultiSignatureSignedFlag(id);
            return true;
        }
        finally
        {
            Lock.ReleaseWriterLock();
        }
    }

    private MultiSignature AddOrUpdateMultiSignature(ChainConfig chainConfig, string id, byte[] msg,
        PartialSignatureDto partialSign)
    {
        var sign = _stateProvider.GetMultiSignature(id) ?? new MultiSignature(
            ByteArrayHelper.HexStringToByteArray(chainConfig.SignerSecret),
            msg, chainConfig.DistPublicKey, chainConfig.PartialSignaturesThreshold);

        // todo: skip process owner partial sign
        if (!sign.ProcessPartialSignature(partialSign)) return sign;

        _stateProvider.SetMultiSignature(id, sign);

        return sign;
    }

    private async Task ProcessTransactionResultAsync(GenerateMultiSignatureJobArgs args, string transactionId,
        RequestDto request)
    {
        var txResult = _objectMapper.Map<GenerateMultiSignatureJobArgs, CommitTransmitResultRequest>(args);
        txResult.TransmitTransactionId = transactionId;

        var finishArgs = _objectMapper.Map<RequestDto, TransmitResultProcessJobArgs>(request);
        finishArgs.TransactionId = transactionId;

        await _backgroundJobManager.EnqueueAsync(args);

        var context = new CancellationTokenSource(TimeSpan.FromSeconds(GrpcConstants.DefaultRequestTimeout));
        await _peerManager.BroadcastAsync(p => p.CommitTransmitResultAsync(txResult, cancellationToken: context.Token));
    }

    private string GenerateMultiSignatureId(GenerateMultiSignatureJobArgs args)
    {
        return IdGeneratorHelper.GenerateId(args.ChainId, args.RequestId, args.Epoch, args.RoundId);
    }
}