using System;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Common.ContractHandler;
using AetherLink.Worker.Core.Consts;
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

public class FinishedProcessJob : AsyncBackgroundJob<FinishedProcessJobArgs>, ITransientDependency
{
    private readonly ILogger<FinishedProcessJob> _logger;
    private readonly IContractProvider _contractProvider;
    private readonly ISchedulerService _schedulerService;
    private readonly ProcessJobOptions _processJobOptions;
    private readonly IJobRequestProvider _jobRequestProvider;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public FinishedProcessJob(ILogger<FinishedProcessJob> logger, IJobRequestProvider jobRequestProvider,
        IContractProvider contractProvider, IBackgroundJobManager backgroundJobManager,
        ISchedulerService schedulerService, IOptionsSnapshot<ProcessJobOptions> processJobOptions)
    {
        _logger = logger;
        _contractProvider = contractProvider;
        _schedulerService = schedulerService;
        _jobRequestProvider = jobRequestProvider;
        _processJobOptions = processJobOptions.Value;
        _backgroundJobManager = backgroundJobManager;
    }

    public override async Task ExecuteAsync(FinishedProcessJobArgs args)
    {
        var chainId = args.ChainId;
        var reqId = args.RequestId;
        var roundId = args.RoundId;
        var epoch = args.Epoch;
        var transactionId = args.TransactionId;
        try
        {
            _logger.LogInformation("[Step6] Get transmitted broadcast. reqId:{ReqId}, transactionId:{transactionId}",
                reqId, transactionId);

            var request = await _jobRequestProvider.GetJobRequestAsync(chainId, reqId, epoch);
            if (request == null)
            {
                _logger.LogWarning("[Step6] Request is not exist. reqId:{ReqId}, roundId:{Round}, epoch:{Epoch}",
                    reqId, roundId, epoch);
                return;
            }

            var txResult = await _contractProvider.GetTxResultAsync(chainId, transactionId);
            if (txResult.Status == TransactionState.Pending)
            {
                _logger.LogWarning(
                    "[Step6] Request {ReqId} transactionId {transactionId} is not mined, will retry in {time}.", reqId,
                    transactionId, _processJobOptions.TransactionResultDefaultDelayTime);
                await _backgroundJobManager.EnqueueAsync(args,
                    delay: TimeSpan.FromSeconds(_processJobOptions.TransactionResultDefaultDelayTime));
                return;
            }

            // for canceled request 
            if (!string.IsNullOrEmpty(txResult.Error) && txResult.Error.Contains("Invalid request id"))
            {
                _logger.LogWarning("[Step6] Request {ReqId} is canceled.", reqId);
                _schedulerService.CancelScheduler(request, SchedulerType.CheckTransmitScheduler);
                _schedulerService.CancelScheduler(request, SchedulerType.CheckRequestEndScheduler);
                return;
            }

            // When Transaction status is not mined or pending, Transaction is judged to be failed.
            if (txResult.Status != TransactionState.Mined)
            {
                _logger.LogWarning("[Step6] Request {ReqId} is {state} not Mined", reqId, txResult.Status);
                // todo: add tx fail execution.
                return;
            }

            if (reqId != _contractProvider.GetTransmitted(txResult).RequestId.ToHex())
            {
                _logger.LogWarning("[Step6] Request {ReqId} transactionId {transactionId} not match.", reqId,
                    transactionId);
                return;
            }

            request.State = RequestState.Transmitted;
            await _jobRequestProvider.SetJobRequestAsync(request);

            _schedulerService.CancelScheduler(request, SchedulerType.CheckTransmitScheduler);
            _schedulerService.CancelScheduler(request, SchedulerType.CheckRequestEndScheduler);

            _logger.LogInformation(
                "[FinishedProcessJob] {ReqId}-{epoch}-{round} Transmitted validate successful, took {time} ms.",
                reqId, epoch, roundId, DateTime.Now.Subtract(request.RequestStartTime).TotalMilliseconds);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[FinishedProcessJob] Failed, reqId:{ReqId}, epoch:{Epoch}", reqId, epoch);
        }
    }
}