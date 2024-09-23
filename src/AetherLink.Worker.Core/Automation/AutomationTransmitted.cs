using System;
using System.Threading.Tasks;
using AetherLink.Contracts.Automation;
using AetherLink.Worker.Core.Automation.Args;
using AetherLink.Worker.Core.Automation.Providers;
using AetherLink.Worker.Core.Common.ContractHandler;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Scheduler;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Automation;

public class AutomationTransmitted : AsyncBackgroundJob<BroadcastTransmitResultArgs>, ITransientDependency
{
    private readonly IJobProvider _jobProvider;
    private readonly IRetryProvider _retryProvider;
    private readonly IStorageProvider _storageProvider;
    private readonly IContractProvider _contractProvider;
    private readonly ISchedulerService _schedulerService;
    private readonly ILogger<AutomationTransmitted> _logger;
    private readonly IOracleContractProvider _oracleContractProvider;

    public AutomationTransmitted(ILogger<AutomationTransmitted> logger, IOracleContractProvider oracleContractProvider,
        ISchedulerService schedulerService, IRetryProvider retryProvider, IContractProvider contractProvider,
        IJobProvider jobProvider, IStorageProvider storageProvider)
    {
        _logger = logger;
        _jobProvider = jobProvider;
        _retryProvider = retryProvider;
        _storageProvider = storageProvider;
        _contractProvider = contractProvider;
        _schedulerService = schedulerService;
        _oracleContractProvider = oracleContractProvider;
    }

    public override async Task ExecuteAsync(BroadcastTransmitResultArgs args)
    {
        var chainId = args.Context.ChainId;
        var upkeepId = args.Context.RequestId;
        var epoch = args.Context.Epoch;
        var transactionId = args.TransactionId;

        try
        {
            _logger.LogInformation($"Get transmitted args. upkeepId:{upkeepId}, transactionId:{transactionId}");

            var txResult = await _contractProvider.GetTxResultAsync(chainId, transactionId);
            switch (txResult.Status)
            {
                case TransactionState.Mined:
                    try
                    {
                        var commitment = await _oracleContractProvider.GetRequestCommitmentAsync(chainId, upkeepId);

                        switch (AutomationHelper.GetTriggerType(commitment))
                        {
                            case TriggerType.Cron:
                                var transmitted = _contractProvider.ParseTransmitted(txResult);
                                var job = await _jobProvider.GetAsync(args.Context);
                                if (job == null || job.State is RequestState.RequestCanceled) return;
                                if (upkeepId != transmitted.RequestId.ToHex() ||
                                    transmitted.EpochAndRound < job.Epoch) break;

                                _schedulerService.CancelCronUpkeep(job);

                                break;
                            case TriggerType.Log:
                                var report = await _oracleContractProvider.GetTransmitReportByTransactionIdAsync(
                                    chainId, args.TransactionId);
                                var payload = LogTriggerCheckData.Parser.ParseFrom(report.Result);
                                var id = AutomationHelper.GetLogTriggerKeyByPayload(chainId, upkeepId,
                                    payload.ToByteArray());
                                var logTriggerInfo = await _storageProvider.GetAsync<LogTriggerDto>(id);
                                if (logTriggerInfo == null)
                                {
                                    _logger.LogError($"Get non-exist trigger {id} from leader");
                                    return;
                                }

                                _schedulerService.CancelLogUpkeep(logTriggerInfo);

                                break;
                        }
                    }
                    finally
                    {
                        _logger.LogInformation($"Check transaction {transactionId} result succeeded.");
                    }

                    break;
                case TransactionState.Pending:
                    await _retryProvider.RetryAsync(args.Context, args, true, delay: RetryConstants.DefaultDelay);

                    break;
                case TransactionState.NotExisted:
                    _logger.LogDebug($"Transaction {transactionId} not exist.");
                    await _retryProvider.RetryAsync(args.Context, args, backOff: true);

                    break;
                default:
                    _logger.LogError($"Get transaction error: {txResult.Error}");
                    if (txResult.Error.Contains("Invalid request id"))
                        _logger.LogInformation($"upkeep: {upkeepId} cancelled");
                    // _schedulerService.CancelAllSchedule(job);

                    break;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[Automation]Check transmit args failed, upkeep:{upkeepId}, epoch:{epoch}");
        }
    }
}