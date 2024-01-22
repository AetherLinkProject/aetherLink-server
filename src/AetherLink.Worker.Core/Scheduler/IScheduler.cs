using System;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using Volo.Abp.DependencyInjection;
using FluentScheduler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AetherLink.Worker.Core.Scheduler;

public interface ISchedulerService
{
    public void StartScheduler(RequestDto request, SchedulerType type);
    public void CancelScheduler(RequestDto request, SchedulerType type);
    public void CancelAllSchedule(RequestDto request);
    public DateTime UpdateBlockTime(DateTime blockStartTime);
}

public class SchedulerService : ISchedulerService, ISingletonDependency
{
    private readonly SchedulerOptions _options;
    private readonly ILogger<SchedulerService> _logger;
    private readonly IResetRequestSchedulerJob _resetRequestScheduler;
    private readonly IObservationCollectSchedulerJob _observationScheduler;

    public SchedulerService(IResetRequestSchedulerJob resetRequestScheduler, ILogger<SchedulerService> logger,
        IOptionsSnapshot<SchedulerOptions> schedulerOptions, IObservationCollectSchedulerJob observationScheduler)
    {
        _logger = logger;
        _options = schedulerOptions.Value;
        _observationScheduler = observationScheduler;
        _resetRequestScheduler = resetRequestScheduler;
        ListenForStart();
        ListenForEnd();
    }

    public void StartScheduler(RequestDto request, SchedulerType type)
    {
        JobManager.UseUtcTime();
        DateTime overTime;
        var registry = new Registry();
        var schedulerName = GenerateScheduleName(request.ChainId, request.RequestId, type);
        CancelSchedulerByName(schedulerName);

        switch (type)
        {
            case SchedulerType.ObservationCollectWaitingScheduler:
                overTime = DateTime.Now.AddMinutes(_options.ObservationCollectTimeoutWindow);
                registry.Schedule(() => _observationScheduler.Execute(request)).WithName(schedulerName).NonReentrant()
                    .ToRunOnceAt(overTime);
                break;
            case SchedulerType.CheckRequestEndScheduler:
                overTime = request.RequestReceiveTime.AddMinutes(_options.CheckRequestEndTimeoutWindow);
                registry.Schedule(() => _resetRequestScheduler.Execute(request)).WithName(schedulerName)
                    .NonReentrant().ToRunOnceAt(overTime);
                break;
            default:
                overTime = DateTime.MinValue;
                break;
        }

        if (DateTime.UtcNow > overTime) return;
        _logger.LogDebug("[SchedulerService] Registry scheduler {name} OverTime:{overTime}", schedulerName, overTime);
        JobManager.Initialize(registry);
    }

    public void CancelScheduler(RequestDto request, SchedulerType type)
    {
        CancelSchedulerByName(GenerateScheduleName(request.ChainId, request.RequestId, type));
    }

    public void CancelAllSchedule(RequestDto request)
    {
        foreach (SchedulerType schedulerType in Enum.GetValues(typeof(SchedulerType)))
        {
            CancelSchedulerByName(GenerateScheduleName(request.ChainId, request.RequestId, schedulerType));
        }
    }

    private void CancelSchedulerByName(string schedulerName)
    {
        var scheduler = JobManager.GetSchedule(schedulerName);
        if (scheduler == null) return;

        _logger.LogDebug("[SchedulerService] Ready to cancel scheduler: {Name}", schedulerName);
        scheduler.Disable();
        JobManager.RemoveJob(schedulerName);
    }

    private void ListenForStart()
    {
        JobManager.JobStart += info => _logger.LogInformation("{Name}: started", info.Name);
    }

    private void ListenForEnd()
    {
        JobManager.JobEnd += info => _logger.LogInformation("{Name}: ended", info.Name);
    }

    private static string GenerateScheduleName(string chainId, string requestId, SchedulerType scheduleType)
    {
        return IdGeneratorHelper.GenerateId(chainId, requestId, scheduleType);
    }

    public DateTime UpdateBlockTime(DateTime blockStartTime)
    {
        var nowTime = DateTime.Now;
        while (true)
        {
            // block += 30 < nowTime, need add 30min continue
            var temp = blockStartTime.AddSeconds(SchedulerTimeConstants.MaximumTimeoutWindow);
            if (temp < nowTime)
            {
                blockStartTime = temp;
                continue;
            }

            // block += 10 < nowTime, need add 10min continue
            temp = blockStartTime.AddSeconds(SchedulerTimeConstants.MediumTimeoutWindow);
            if (temp < nowTime)
            {
                blockStartTime = temp;
                continue;
            }

            // block += 5 > nowTime, no need add, return
            temp = blockStartTime.AddSeconds(SchedulerTimeConstants.SmallTimeoutWindow);
            if (temp > nowTime)
            {
                return blockStartTime;
            }

            //  nowTime < block+5 < nowTime+10, nowTime+10 is RequestReceive time window
            blockStartTime = temp;
        }
    }
}