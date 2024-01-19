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
    private readonly object _lock;
    private readonly IPeerManager _peerManager;
    private readonly OracleInfoOptions _options;
    private readonly IObjectMapper _objectMapper;
    private readonly IStateProvider _stateProvider;
    private readonly IReportProvider _reportProvider;
    private readonly IRequestProvider _requestProvider;
    private readonly IContractProvider _contractProvider;
    private readonly ILogger<GenerateMultiSignatureJob> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IOracleContractProvider _oracleContractProvider;

    public GenerateMultiSignatureJob(ILogger<GenerateMultiSignatureJob> logger, IStateProvider stateProvider,
        IOptionsSnapshot<OracleInfoOptions> oracleChainInfoOptions, IContractProvider contractProvider,
        IOracleContractProvider oracleContractProvider, IObjectMapper objectMapper, IRequestProvider requestProvider,
        IReportProvider reportProvider, IPeerManager peerManager, IBackgroundJobManager backgroundJobManager)
    {
        _logger = logger;
        _lock = new object();
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

            TryProcessMultiSignature(args, msg);

            if (!IsSignatureEnough(args) ||
                _stateProvider.GetMultiSignatureSignedFlag(GenerateMultiSignatureId(args))) return;

            _stateProvider.SetMultiSignatureSignedFlag(GenerateMultiSignatureId(args));
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

    private bool IsSignatureEnough(GenerateMultiSignatureJobArgs args)
    {
        var sign = _stateProvider.GetMultiSignature(GenerateMultiSignatureId(args));
        return sign != null && sign.IsEnoughPartialSig();
    }

    private void TryProcessMultiSignature(GenerateMultiSignatureJobArgs args, byte[] msg)
    {
        if (!_options.ChainConfig.TryGetValue(args.ChainId, out var config)) return;
        lock (_lock)
        {
            var id = GenerateMultiSignatureId(args);
            var sign = _stateProvider.GetMultiSignature(id);
            if (sign == null)
            {
                _stateProvider.SetMultiSignature(id, new MultiSignature(
                    ByteArrayHelper.HexStringToByteArray(config.SignerSecret),
                    msg, config.DistPublicKey, config.PartialSignaturesThreshold));
                return;
            }

            if (sign.ProcessPartialSignature(args.PartialSignature)) _stateProvider.SetMultiSignature(id, sign);
        }
    }

    private async Task ProcessTransactionResultAsync(GenerateMultiSignatureJobArgs args, string transactionId,
        RequestDto request)
    {
        var finishArgs = _objectMapper.Map<RequestDto, TransmitResultProcessJobArgs>(request);
        finishArgs.TransactionId = transactionId;
        await _backgroundJobManager.EnqueueAsync(finishArgs);

        var txResult = _objectMapper.Map<GenerateMultiSignatureJobArgs, CommitTransmitResultRequest>(args);
        txResult.TransmitTransactionId = transactionId;
        var context = new CancellationTokenSource(TimeSpan.FromSeconds(GrpcConstants.DefaultRequestTimeout));
        await _peerManager.BroadcastAsync(p => p.CommitTransmitResultAsync(txResult, cancellationToken: context.Token));
    }

    private string GenerateMultiSignatureId(GenerateMultiSignatureJobArgs args)
    {
        return IdGeneratorHelper.GenerateId(args.ChainId, args.RequestId, args.Epoch, args.RoundId);
    }
}