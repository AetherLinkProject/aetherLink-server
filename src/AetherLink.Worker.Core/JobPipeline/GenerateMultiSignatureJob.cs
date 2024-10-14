using AElf;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
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
using AetherLink.Worker.Core.Reporter;
using Newtonsoft.Json;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.JobPipeline;

public class GenerateMultiSignatureJob : AsyncBackgroundJob<GenerateMultiSignatureJobArgs>, ISingletonDependency
{
    private readonly object _lock = new();
    private readonly object _finishLock = new();
    private readonly IPeerManager _peerManager;
    private readonly IJobProvider _jobProvider;
    private readonly OracleInfoOptions _options;
    private readonly IObjectMapper _objectMapper;
    private readonly IStateProvider _stateProvider;
    private readonly IReportProvider _reportProvider;
    private readonly IMultiSignatureReporter _reporter;
    private readonly IContractProvider _contractProvider;
    private readonly IDataMessageProvider _dataMessageProvider;
    private readonly ILogger<GenerateMultiSignatureJob> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IOracleContractProvider _oracleContractProvider;

    public GenerateMultiSignatureJob(ILogger<GenerateMultiSignatureJob> logger, IStateProvider stateProvider,
        IOptionsSnapshot<OracleInfoOptions> oracleChainInfoOptions, IContractProvider contractProvider,
        IOracleContractProvider oracleContractProvider, IObjectMapper objectMapper, IJobProvider jobProvider,
        IReportProvider reportProvider, IPeerManager peerManager, IBackgroundJobManager backgroundJobManager,
        IMultiSignatureReporter reporter, IDataMessageProvider dataMessageProvider)
    {
        _logger = logger;
        _reporter = reporter;
        _jobProvider = jobProvider;
        _peerManager = peerManager;
        _objectMapper = objectMapper;
        _stateProvider = stateProvider;
        _reportProvider = reportProvider;
        _contractProvider = contractProvider;
        _options = oracleChainInfoOptions.Value;
        _dataMessageProvider = dataMessageProvider;
        _backgroundJobManager = backgroundJobManager;
        _oracleContractProvider = oracleContractProvider;
    }

    public override async Task ExecuteAsync(GenerateMultiSignatureJobArgs args)
    {
        await Handler(args);
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(GenerateMultiSignatureJob),
        MethodName = nameof(HandleException))]
    public virtual async Task Handler(GenerateMultiSignatureJobArgs args)
    {
        var reqId = args.RequestId;
        var chainId = args.ChainId;
        var roundId = args.RoundId;
        var epoch = args.Epoch;
        var argId = IdGeneratorHelper.GenerateId(chainId, reqId, epoch, roundId);

        var job = await _jobProvider.GetAsync(args);
        if (job == null || job.State == RequestState.RequestCanceled) return;

        var multiSignId = IdGeneratorHelper.GenerateMultiSignatureId(args);
        if (_stateProvider.IsFinished(multiSignId)) return;

        var jobSpec = JsonConvert.DeserializeObject<DataFeedsDto>(job.JobSpec).DataFeedsJobSpec;
        ByteString result;

        if (jobSpec.Type == DataFeedsType.PlainDataFeeds)
        {
            var authData = await _dataMessageProvider.GetPlainDataFeedsAsync(args);
            if (authData == null)
            {
                _logger.LogError("[Step5][Leader] {name} Report is null.", argId);
                return;
            }

            result = ByteString.CopyFrom(Encoding.UTF8.GetBytes(authData.NewData));
        }
        else
        {
            var observations = await _reportProvider.GetAsync(args);
            if (observations == null)
            {
                _logger.LogError("[Step5][Leader] {name} Report is null.", argId);
                return;
            }

            result = new LongList { Data = { observations.Observations } }.ToByteString();
        }

        var transmitData = await _oracleContractProvider.GenerateTransmitDataAsync(chainId, reqId, epoch, result);
        var msg = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(transmitData.Report.ToByteArray()),
            HashHelper.ComputeFrom(transmitData.ReportContext.ToString())).ToByteArray();

        TryProcessMultiSignature(args, msg);

        if (!IsSignatureEnough(args))
        {
            _logger.LogDebug("[Step5][Leader] {name} is not enough, no need to generate signature.", argId);
            return;
        }

        if (!TryProcessFinishedFlag(multiSignId))
        {
            _logger.LogDebug("[Step5][Leader] {name} signature is finished.", argId);
            return;
        }

        _logger.LogInformation("[Step5][Leader] {name} MultiSignature generate success.", argId);

        var multiSignature = _stateProvider.GetMultiSignature(multiSignId);
        multiSignature.TryGetSignatures(out var signature);
        transmitData.Signatures.AddRange(signature);

        // send transmit transaction to oracle contract
        var transactionId = await _contractProvider.SendTransmitAsync(chainId, transmitData);
        _logger.LogInformation("[step5][Leader] {name} Transmit transaction {transactionId}", argId,
            transactionId);

        await ProcessTransactionResultAsync(args, transactionId, job);
    }

    private bool IsSignatureEnough(GenerateMultiSignatureJobArgs args)
    {
        var sign = _stateProvider.GetMultiSignature(IdGeneratorHelper.GenerateMultiSignatureId(args));
        return sign != null && sign.IsEnoughPartialSig();
    }

    private bool TryProcessFinishedFlag(string signatureId)
    {
        lock (_finishLock)
        {
            if (_stateProvider.IsFinished(signatureId)) return false;
            _stateProvider.SetFinishedFlag(signatureId);
            return true;
        }
    }

    private void TryProcessMultiSignature(GenerateMultiSignatureJobArgs args, byte[] msg)
    {
        lock (_lock)
        {
            if (!_options.ChainConfig.TryGetValue(args.ChainId, out var config))
            {
                throw new InvalidDataException($"Not support chain {args.ChainId}.");
            }

            var id = IdGeneratorHelper.GenerateMultiSignatureId(args);
            var sign = _stateProvider.GetMultiSignature(id);
            if (sign == null)
            {
                _logger.LogDebug("[Step5] {index} init multi signature.", args.PartialSignature.Index);
                var newMultiSignature = new MultiSignature(ByteArrayHelper.HexStringToByteArray(config.SignerSecret),
                    msg, config.DistPublicKey, config.PartialSignaturesThreshold);
                newMultiSignature.GeneratePartialSignature();
                _stateProvider.SetMultiSignature(id, newMultiSignature);
                _reporter.RecordMultiSignatureAsync(args.ChainId, args.RequestId, args.Epoch);
                return;
            }

            if (!sign.ProcessPartialSignature(args.PartialSignature))
            {
                _logger.LogDebug("[Step5] {index} Process multi signature failed", args.PartialSignature.Index);
                _reporter.RecordMultiSignatureProcessResultAsync(args.ChainId, args.RequestId, args.Epoch,
                    args.PartialSignature.Index, "failed");
                return;
            }

            _stateProvider.SetMultiSignature(id, sign);
            _reporter.RecordMultiSignatureAsync(args.ChainId, args.RequestId, args.Epoch);
        }
    }

    private async Task ProcessTransactionResultAsync(GenerateMultiSignatureJobArgs args, string transactionId,
        JobDto job)
    {
        var finishArgs = _objectMapper.Map<JobDto, TransmitResultProcessJobArgs>(job);
        finishArgs.TransactionId = transactionId;
        await _backgroundJobManager.EnqueueAsync(finishArgs);

        var txResult = _objectMapper.Map<GenerateMultiSignatureJobArgs, CommitTransmitResultRequest>(args);
        txResult.TransmitTransactionId = transactionId;
        await _peerManager.BroadcastAsync(p => p.CommitTransmitResultAsync(txResult));
        _reporter.RecordMultiSignatureProcessResultAsync(args.ChainId, args.RequestId, args.Epoch,
            args.PartialSignature.Index);
    }

    #region Exception Handing

    public async Task<FlowBehavior> HandleException(Exception ex, GenerateMultiSignatureJobArgs args)
    {
        var argId = IdGeneratorHelper.GenerateId(args.ChainId, args.RequestId, args.Epoch, args.RoundId);
        _logger.LogError(ex, "[Step5][Leader] {name} SendTransaction Failed.", argId);

        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return
        };
    }

    #endregion
}