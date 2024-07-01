using System;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Automation.Args;
using AetherLink.Worker.Core.Common.ContractHandler;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Scheduler;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Automation;

public class AutomationTransmitted : AsyncBackgroundJob<BroadcastTransmitResultArgs>, ITransientDependency
{
    private readonly IJobProvider _jobProvider;
    private readonly IRetryProvider _retryProvider;
    private readonly IContractProvider _contractProvider;
    private readonly ISchedulerService _schedulerService;
    private readonly ILogger<AutomationTransmitted> _logger;

    public AutomationTransmitted(ILogger<AutomationTransmitted> logger, IContractProvider contractProvider,
        ISchedulerService schedulerService, IRetryProvider retryProvider, IJobProvider jobProvider)
    {
        _logger = logger;
        _jobProvider = jobProvider;
        _retryProvider = retryProvider;
        _contractProvider = contractProvider;
        _schedulerService = schedulerService;
    }

    public override async Task ExecuteAsync(BroadcastTransmitResultArgs result)
    {
        var chainId = result.Context.ChainId;
        var reqId = result.Context.RequestId;
        var epoch = result.Context.Epoch;
        var transactionId = result.TransactionId;

        try
        {
            _logger.LogInformation("Get transmitted result. reqId:{ReqId}, transactionId:{txId}",
                reqId, transactionId);

            var job = await _jobProvider.GetAsync(result.Context);
            if (job == null || job.State is RequestState.RequestCanceled || job.Epoch > epoch) return;

            var txResult = await _contractProvider.GetTxResultAsync(chainId, transactionId);
            switch (txResult.Status)
            {
                case TransactionState.Mined:
                    var transmitted = _contractProvider.ParseTransmitted(txResult);
                    if (reqId != transmitted.RequestId.ToHex() || transmitted.EpochAndRound < job.Epoch) break;
                    _schedulerService.CancelAllSchedule(job);

                    break;
                case TransactionState.Pending:
                    await _retryProvider.RetryAsync(result.Context, result, true, delay: RetryConstants.DefaultDelay);

                    break;
                case TransactionState.NotExisted:
                    await _retryProvider.RetryAsync(result.Context, result, backOff: true);

                    break;
                default:
                    if (txResult.Error.Contains("Invalid request id")) _schedulerService.CancelAllSchedule(job);

                    break;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Automation]Check transmit result failed, reqId:{ReqId}, epoch:{Epoch}", reqId, epoch);
        }
    }
}