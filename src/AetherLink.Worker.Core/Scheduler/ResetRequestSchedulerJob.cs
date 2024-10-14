using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.Scheduler;

public interface IResetRequestSchedulerJob
{
    public Task Execute(JobDto job);
}

public class ResetRequestSchedulerJob : IResetRequestSchedulerJob, ITransientDependency
{
    private readonly IObjectMapper _objectMapper;
    private readonly IJobProvider _jobProvider;
    private readonly SchedulerOptions _schedulerOptions;
    private readonly ILogger<ResetRequestSchedulerJob> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public ResetRequestSchedulerJob(IBackgroundJobManager backgroundJobManager, IObjectMapper objectMapper,
        ILogger<ResetRequestSchedulerJob> logger, IOptionsSnapshot<SchedulerOptions> schedulerOptions,
        IJobProvider jobProvider)
    {
        _logger = logger;
        _objectMapper = objectMapper;
        _jobProvider = jobProvider;
        _schedulerOptions = schedulerOptions.Value;
        _backgroundJobManager = backgroundJobManager;
    }

    [ExceptionHandler(typeof(Exception), Message = "[ResetScheduler] Reset scheduler job failed.")]
    public virtual async Task Execute(JobDto job)
    {
        if (job.State == RequestState.RequestCanceled) return;

        _logger.LogInformation(
            "[ResetScheduler] Scheduler job execute. reqId {ReqId}, roundId:{RoundId}, reqState:{State}",
            job.RequestId, job.RoundId, job.State.ToString());
        job.RoundId++;

        while (DateTime.UtcNow > DateTimeOffset.FromUnixTimeMilliseconds(job.TransactionBlockTime).DateTime)
        {
            job.TransactionBlockTime += _schedulerOptions.RetryTimeOut * 60 * 1000;
        }

        _logger.LogDebug("[ResetScheduler] blockTime {time}", job.TransactionBlockTime);

        await _jobProvider.SetAsync(job);

        var hangfireJobId = await _backgroundJobManager.EnqueueAsync(
            _objectMapper.Map<JobDto, RequestStartProcessJobArgs>(job), BackgroundJobPriority.High);
        _logger.LogInformation(
            "[ResetScheduler] Request {ReqId} timeout, will starting in new round:{RoundId}, hangfireId:{hangfire}",
            job.RequestId, job.RoundId, hangfireJobId);
    }
}