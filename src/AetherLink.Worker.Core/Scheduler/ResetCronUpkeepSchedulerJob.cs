using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AetherLink.Worker.Core.Automation.Args;
using AetherLink.Worker.Core.Automation.Providers;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.Scheduler;

public interface IResetCronUpkeepSchedulerJob
{
    public Task Execute(JobDto job);
}

public class ResetCronUpkeepSchedulerJob : IResetCronUpkeepSchedulerJob, ITransientDependency
{
    private readonly IJobProvider _jobProvider;
    private readonly IObjectMapper _objectMapper;
    private readonly IStorageProvider _storageProvider;
    private readonly SchedulerOptions _schedulerOptions;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ILogger<ResetCronUpkeepSchedulerJob> _logger;


    public ResetCronUpkeepSchedulerJob(IBackgroundJobManager backgroundJobManager,
        ILogger<ResetCronUpkeepSchedulerJob> logger, IOptionsSnapshot<SchedulerOptions> schedulerOptions,
        IStorageProvider storageProvider, IJobProvider jobProvider, IObjectMapper objectMapper)
    {
        _logger = logger;
        _jobProvider = jobProvider;
        _objectMapper = objectMapper;
        _storageProvider = storageProvider;
        _schedulerOptions = schedulerOptions.Value;
        _backgroundJobManager = backgroundJobManager;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ResetCronUpkeepSchedulerJob),
        MethodName = nameof(HandleException))]
    public virtual async Task Execute(JobDto job)
    {
        if (job.State == RequestState.RequestCanceled) return;

        _logger.LogInformation(
            "[CronUpkeep] Scheduler job execute. reqId {ReqId}, roundId:{RoundId}, reqState:{State}",
            job.RequestId, job.RoundId, job.State.ToString());
        job.RoundId++;

        while (DateTime.UtcNow > DateTimeOffset.FromUnixTimeMilliseconds(job.TransactionBlockTime).DateTime)
        {
            job.TransactionBlockTime += _schedulerOptions.RetryTimeOut * 60 * 1000;
        }

        _logger.LogDebug("[CronUpkeep] blockTime {time}", job.TransactionBlockTime);

        await _jobProvider.SetAsync(job);

        var hangfireJobId =
            await _backgroundJobManager.EnqueueAsync(_objectMapper.Map<JobDto, AutomationStartJobArgs>(job),
                BackgroundJobPriority.High);
        _logger.LogInformation(
            "[CronUpkeep] Request {ReqId} timeout, will starting in new round:{RoundId}, hangfireId:{hangfire}",
            job.RequestId, job.RoundId, hangfireJobId);
    }

    #region Exception handing

    public async Task<FlowBehavior> HandleException(Exception ex)
    {
        _logger.LogError(ex, "[CronUpkeep] Reset scheduler job failed.");

        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return
        };
    }

    #endregion
}