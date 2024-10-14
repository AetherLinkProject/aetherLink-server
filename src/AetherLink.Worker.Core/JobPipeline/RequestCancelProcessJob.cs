using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AetherLink.Contracts.Automation;
using AetherLink.Worker.Core.Automation.Providers;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Scheduler;
using Hangfire;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.JobPipeline;

public class RequestCancelProcessJob : AsyncBackgroundJob<RequestCancelProcessJobArgs>, ITransientDependency
{
    private readonly IJobProvider _jobProvider;
    private readonly IFilterStorage _filterStorage;
    private readonly IStorageProvider _storageProvider;
    private readonly ISchedulerService _schedulerService;
    private readonly ILogger<RequestCancelProcessJob> _logger;
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly IOracleContractProvider _oracleContractProvider;

    public RequestCancelProcessJob(IRecurringJobManager recurringJobManager, ILogger<RequestCancelProcessJob> logger,
        ISchedulerService schedulerService, IJobProvider jobProvider, IOracleContractProvider oracleContractProvider,
        IFilterStorage filterStorage, IStorageProvider storageProvider)
    {
        _logger = logger;
        _jobProvider = jobProvider;
        _filterStorage = filterStorage;
        _storageProvider = storageProvider;
        _schedulerService = schedulerService;
        _recurringJobManager = recurringJobManager;
        _oracleContractProvider = oracleContractProvider;
    }

    public override async Task ExecuteAsync(RequestCancelProcessJobArgs args)
    {
        await Handler(args);
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(RequestCancelProcessJob),
        MethodName = nameof(HandleException), FinallyMethodName = nameof(FinallyHandler))]
    public virtual async Task Handler(RequestCancelProcessJobArgs args)
    {
        var chainId = args.ChainId;
        var requestId = args.RequestId;

        _logger.LogInformation($"[RequestCancelProcess] {chainId} {requestId} Start cancel job.");

        var commitment = await _oracleContractProvider.GetRequestCommitmentAsync(chainId, requestId);
        if (commitment.RequestTypeIndex == RequestTypeConst.Automation &&
            AutomationHelper.GetTriggerType(commitment) == TriggerType.Log)
        {
            var logUpkeepInfoId = IdGeneratorHelper.GenerateUpkeepInfoId(chainId, requestId);
            var logUpkeepInfo = await _storageProvider.GetAsync<LogUpkeepInfoDto>(logUpkeepInfoId);
            _schedulerService.CancelLogUpkeepAllSchedule(logUpkeepInfo);

            await _filterStorage.DeleteFilterAsync(logUpkeepInfo);
            await _storageProvider.RemoveAsync(logUpkeepInfoId);
            return;
        }

        _recurringJobManager.RemoveIfExists(IdGeneratorHelper.GenerateId(chainId, requestId));
        var job = await _jobProvider.GetAsync(args);
        if (job == null)
        {
            _logger.LogInformation($"[RequestCancelProcess] {chainId} {requestId} not exist");
            return;
        }

        if (commitment.RequestTypeIndex == RequestTypeConst.Automation) _schedulerService.CancelCronUpkeep(job);

        job.State = RequestState.RequestCanceled;
        await _jobProvider.SetAsync(job);
        _schedulerService.CancelAllSchedule(job);
    }

    #region Exception Handing

    public async Task<FlowBehavior> HandleException(Exception ex, RequestCancelProcessJobArgs args)
    {
        _logger.LogError(ex, $"[RequestCancelProcess] {args.ChainId} {args.RequestId} Cancel job failed.");

        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return
        };
    }

    public async Task FinallyHandler(RequestCancelProcessJobArgs args)
    {
        // todo: add state cleanup
        _logger.LogInformation($"[RequestCancelProcess] {args.ChainId} {args.RequestId} Cancel job finished.");
    }

    #endregion
}