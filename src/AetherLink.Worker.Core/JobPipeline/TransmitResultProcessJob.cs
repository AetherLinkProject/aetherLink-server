using System;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common.ContractHandler;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Scheduler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.JobPipeline;

public class TransmitResultProcessJob : AsyncBackgroundJob<TransmitResultProcessJobArgs>, ITransientDependency
{
    private readonly IJobProvider _jobProvider;
    private readonly IRetryProvider _retryProvider;
    private readonly IContractProvider _contractProvider;
    private readonly ISchedulerService _schedulerService;
    private readonly ProcessJobOptions _processJobOptions;
    private readonly ILogger<TransmitResultProcessJob> _logger;

    public TransmitResultProcessJob(ILogger<TransmitResultProcessJob> logger, IContractProvider contractProvider,
        IOptionsSnapshot<ProcessJobOptions> processJobOptions, ISchedulerService schedulerService,
        IRetryProvider retryProvider, IJobProvider jobProvider)
    {
        _logger = logger;
        _jobProvider = jobProvider;
        _retryProvider = retryProvider;
        _contractProvider = contractProvider;
        _schedulerService = schedulerService;
        _processJobOptions = processJobOptions.Value;
    }

    public override async Task ExecuteAsync(TransmitResultProcessJobArgs args)
    {
        var chainId = args.ChainId;
        var reqId = args.RequestId;
        var roundId = args.RoundId;
        var epoch = args.Epoch;
        var transactionId = args.TransactionId;
        try
        {
            _logger.LogInformation(
                "[Step6] Get leader transmitted result broadcast. reqId:{ReqId}, transactionId:{transactionId}",
                reqId, transactionId);

            var job = await _jobProvider.GetAsync(args);

            if (job == null || job.State is RequestState.RequestCanceled || job.Epoch > args.Epoch) return;

            var txResult = await _contractProvider.GetTxResultAsync(chainId, transactionId);
            if (txResult.Status is TransactionState.Pending)
            {
                _logger.LogInformation(
                    "[Step6] Request {ReqId} transactionId {transactionId} is not mined, will retry in {time} s.",
                    reqId,
                    transactionId, _processJobOptions.TransactionResultDelay);
                await _retryProvider.RetryAsync(args, true, delayDelta: _processJobOptions.TransactionResultDelay);
                return;
            }

            // for canceled request 
            if (!string.IsNullOrEmpty(txResult.Error) && txResult.Error.Contains("Invalid request id"))
            {
                _logger.LogWarning("[Step6] Request {ReqId} is canceled before transmit.", reqId);
                _schedulerService.CancelAllSchedule(job);
                return;
            }

            // When Transaction status is not mined or pending, Transaction is judged to be failed.
            if (txResult.Status is not TransactionState.Mined)
            {
                _logger.LogWarning("[Step6] Request {ReqId} is {state} not Mined, transactionId {txId} error: {error}",
                    reqId, txResult.Status, transactionId, txResult.Error);
                // todo: add tx fail execution.
                return;
            }

            var transmittedLogEvent = _contractProvider.ParseTransmitted(txResult);
            if (reqId != transmittedLogEvent.RequestId.ToHex() || transmittedLogEvent.EpochAndRound < job.Epoch)
            {
                _logger.LogError("[Step6] Request {ReqId} transactionId {transactionId} not match.", reqId,
                    transactionId);
                return;
            }

            _logger.LogInformation(
                "[Step6] {ReqId}-{epoch}-{round} Transmitted validate successful, job execute total time {time}s.",
                reqId, epoch, roundId, DateTime.Now.Subtract(job.RequestReceiveTime).TotalSeconds);

            _schedulerService.CancelAllSchedule(job);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Step6] Failed, reqId:{ReqId}, epoch:{Epoch}", reqId, epoch);
        }
    }
}