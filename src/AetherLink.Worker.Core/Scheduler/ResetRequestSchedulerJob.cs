using System;
using System.Threading.Tasks;
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
    public Task Execute(RequestDto request);
}

public class ResetRequestSchedulerJob : IResetRequestSchedulerJob, ITransientDependency
{
    private readonly IObjectMapper _objectMapper;
    private readonly IRequestProvider _requestProvider;
    private readonly SchedulerOptions _schedulerOptions;
    private readonly ILogger<ResetRequestSchedulerJob> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public ResetRequestSchedulerJob(IBackgroundJobManager backgroundJobManager, IObjectMapper objectMapper,
        ILogger<ResetRequestSchedulerJob> logger, IOptionsSnapshot<SchedulerOptions> schedulerOptions,
        IRequestProvider requestProvider)
    {
        _logger = logger;
        _objectMapper = objectMapper;
        _requestProvider = requestProvider;
        _schedulerOptions = schedulerOptions.Value;
        _backgroundJobManager = backgroundJobManager;
    }

    public async Task Execute(RequestDto request)
    {
        try
        {
            if (request.State == RequestState.RequestCanceled) return;

            _logger.LogInformation(
                "[ResetScheduler] Scheduler job execute. reqId {ReqId}, roundId:{RoundId}, reqState:{State}",
                request.RequestId, request.RoundId, request.State.ToString());
            request.RoundId++;

            while (DateTime.UtcNow > DateTimeOffset.FromUnixTimeMilliseconds(request.TransactionBlockTime).DateTime)
            {
                request.TransactionBlockTime += _schedulerOptions.RetryTimeOut * 60 * 1000;
            }

            _logger.LogDebug("[ResetScheduler] blockTime {time}", request.TransactionBlockTime);

            await _requestProvider.SetAsync(request);

            var hangfireJobId = await _backgroundJobManager.EnqueueAsync(
                _objectMapper.Map<RequestDto, RequestStartProcessJobArgs>(request), BackgroundJobPriority.High);
            _logger.LogInformation(
                "[ResetScheduler] Request {ReqId} timeout, will starting in new round:{RoundId}, hangfireId:{hangfire}",
                request.RequestId, request.RoundId, hangfireJobId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[ResetScheduler] Reset scheduler job failed.");
        }
    }
}