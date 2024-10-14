using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AetherLink.Contracts.Automation;
using AetherLink.Worker.Core.Automation.Args;
using AetherLink.Worker.Core.Automation.Providers;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.PeerManager;
using AetherLink.Worker.Core.Provider;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Automation;

public class AutomationTransmit : AsyncBackgroundJob<PartialSignatureResponseArgs>, ITransientDependency
{
    private readonly IPeerManager _peerManager;
    private readonly IJobProvider _jobProvider;
    private readonly IStateProvider _stateProvider;
    private readonly IContractProvider _contractProvider;
    private readonly ILogger<AutomationTransmit> _logger;
    private readonly ISignatureProvider _signatureProvider;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IOracleContractProvider _oracleContractProvider;

    public AutomationTransmit(ILogger<AutomationTransmit> logger, IStateProvider stateProvider,
        IContractProvider contractProvider, IOracleContractProvider oracleContractProvider, IPeerManager peerManager,
        IBackgroundJobManager backgroundJobManager, ISignatureProvider signatureProvider, IJobProvider jobProvider)
    {
        _logger = logger;
        _peerManager = peerManager;
        _jobProvider = jobProvider;
        _stateProvider = stateProvider;
        _contractProvider = contractProvider;
        _signatureProvider = signatureProvider;
        _backgroundJobManager = backgroundJobManager;
        _oracleContractProvider = oracleContractProvider;
    }

    public override async Task ExecuteAsync(PartialSignatureResponseArgs args)
    {
        await Handle(args);
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(AutomationTransmit),
        MethodName = nameof(HandleException))]
    public virtual async Task Handle(PartialSignatureResponseArgs args)
    {
        var chainId = args.Context.ChainId;
        var upkeepId = args.Context.RequestId;
        var epoch = args.Context.Epoch;

        _logger.LogDebug($"[Automation] Get {upkeepId} partial signature response.");

        // var job = await _jobProvider.GetAsync(args.Context);
        // if (job == null || job.State == RequestState.RequestCanceled) return;

        var commitment = await _oracleContractProvider.GetRequestCommitmentAsync(chainId, upkeepId);
        var multiSignId = AutomationHelper.GetTriggerType(commitment) switch
        {
            TriggerType.Cron => AutomationHelper.GenerateCronUpkeepId(args.Context),
            TriggerType.Log => AutomationHelper.GenerateLogTriggerId(
                AutomationHelper.GenerateTransactionEventKeyByPayload(args.Payload),
                IdGeneratorHelper.GenerateUpkeepInfoId(chainId, upkeepId)),
            _ => throw new ArgumentOutOfRangeException()
        };

        _logger.LogDebug($"[Automation] Received {multiSignId} upkeep trigger response.");

        if (_stateProvider.IsFinished(multiSignId)) return;
        if (!_signatureProvider.ProcessMultiSignAsync(multiSignId, args.Index, args.Signature))
        {
            _logger.LogError($"[Automation] {multiSignId} process {args.Index} failed.");
            return;
        }

        if (!_stateProvider.GetMultiSignature(multiSignId).IsEnoughPartialSig())
        {
            _logger.LogDebug($"[Automation] {multiSignId} is not enough, no need to generate signature.");
            return;
        }

        if (_stateProvider.IsFinished(multiSignId))
        {
            _logger.LogDebug($"[Automation] {multiSignId} signature is finished.");
            return;
        }

        _stateProvider.SetFinishedFlag(multiSignId);
        _logger.LogInformation($"[Automation] {multiSignId} MultiSignature pre generate success.");

        // todo: generate signature by payload
        var report = AutomationHelper.GetTriggerType(commitment) switch
        {
            TriggerType.Cron => AutomationHelper.GetUpkeepPerformData(commitment),
            TriggerType.Log => LogTriggerCheckData.Parser.ParseFrom(args.Payload).ToByteString(),
            _ => throw new ArgumentOutOfRangeException()
        };

        var transmitData = await _oracleContractProvider.GenerateTransmitDataAsync(chainId, upkeepId, epoch,
            report);
        var multiSignature = _stateProvider.GetMultiSignature(multiSignId);
        multiSignature.TryGetSignatures(out var signature);
        transmitData.Signatures.AddRange(signature);

        var transactionId = await _contractProvider.SendTransmitAsync(chainId, transmitData);
        _logger.LogInformation($"[Automation] {multiSignId} Transmit transaction {transactionId}");

        await _backgroundJobManager.EnqueueAsync(new BroadcastTransmitResultArgs
        {
            Context = args.Context,
            TransactionId = transactionId
        });

        await _peerManager.BroadcastAsync(p => p.BroadcastTransmitResultAsync(new()
        {
            Context = args.Context,
            TransactionId = transactionId
        }));
    }

    #region Exception Handing

    public async Task<FlowBehavior> HandleException(Exception ex, PartialSignatureResponseArgs args)
    {
        _logger.LogError(ex, $"[Automation] {args.Context.RequestId}-{args.Context.Epoch} send transaction Failed.");

        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return
        };
    }

    #endregion
}