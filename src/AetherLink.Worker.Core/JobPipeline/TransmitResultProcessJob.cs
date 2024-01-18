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
    private readonly IRetryProvider _retryProvider;
    private readonly IRequestProvider _requestProvider;
    private readonly IContractProvider _contractProvider;
    private readonly ISchedulerService _schedulerService;
    private readonly ProcessJobOptions _processJobOptions;
    private readonly ILogger<TransmitResultProcessJob> _logger;

    public TransmitResultProcessJob(ILogger<TransmitResultProcessJob> logger, IContractProvider contractProvider,
        IOptionsSnapshot<ProcessJobOptions> processJobOptions, ISchedulerService schedulerService,
        IRetryProvider retryProvider, IRequestProvider requestProvider)
    {
        _logger = logger;
        _retryProvider = retryProvider;
        _requestProvider = requestProvider;
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

            var request = await _requestProvider.GetAsync(args);

            if (request == null || request.State is RequestState.RequestCanceled || request.Epoch > args.Epoch) return;

            var txResult = await _contractProvider.GetTxResultAsync(chainId, transactionId);
            if (txResult.Status is TransactionState.Pending)
            {
                _logger.LogWarning(
                    "[Step6] Request {ReqId} transactionId {transactionId} is not mined, will retry in {time}.", reqId,
                    transactionId, _processJobOptions.TransactionResultDelay);
                await _retryProvider.RetryAsync(args, true, delayDelta: _processJobOptions.TransactionResultDelay);
                return;
            }

            // for canceled request 
            if (!string.IsNullOrEmpty(txResult.Error) && txResult.Error.Contains("Invalid request id"))
            {
                _logger.LogWarning("[Step6] Request {ReqId} is canceled before transmit.", reqId);
                _schedulerService.CancelAllSchedule(request);
                return;
            }

            // When Transaction status is not mined or pending, Transaction is judged to be failed.
            if (txResult.Status is not TransactionState.Mined)
            {
                _logger.LogWarning("[Step6] Request {ReqId} is {state} not Mined", reqId, txResult.Status);
                // todo: add tx fail execution.
                return;
            }

            var transmittedLogEvent = _contractProvider.ParseTransmitted(txResult);
            if (reqId != transmittedLogEvent.RequestId.ToHex() || transmittedLogEvent.EpochAndRound < request.Epoch)
            {
                _logger.LogWarning("[Step6] Request {ReqId} transactionId {transactionId} not match.", reqId,
                    transactionId);
                return;
            }

            _logger.LogInformation(
                "[Step6] {ReqId}-{epoch}-{round} Transmitted validate successful, job execute total time {time}s.",
                reqId, epoch, roundId, DateTime.Now.Subtract(request.RequestReceiveTime).TotalSeconds);

            _schedulerService.CancelAllSchedule(request);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Step6] Failed, reqId:{ReqId}, epoch:{Epoch}", reqId, epoch);
        }
    }
}