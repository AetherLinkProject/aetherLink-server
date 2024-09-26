using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using Volo.Abp.DependencyInjection;
using FluentScheduler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUglify.Helpers;

namespace AetherLink.Worker.Core.Scheduler;

public interface ISchedulerService
{
    public void StartScheduler(JobDto job, SchedulerType type);
    public void StartScheduler(LogTriggerDto upkeep);
    public void StartScheduler(RampMessageDto rampMessage);
    public void StartCronUpkeepScheduler(JobDto job);
    public void CancelScheduler(JobDto job, SchedulerType type);
    public void CancelLogUpkeep(LogTriggerDto upkeep);
    public void CancelCronUpkeep(JobDto job);
    public void CancelAllSchedule(JobDto job);
    public void CancelLogUpkeepAllSchedule(LogUpkeepInfoDto upkeep);
    public DateTime UpdateBlockTime(DateTime blockStartTime);
}

public class SchedulerService : ISchedulerService, ISingletonDependency
{
    private readonly SchedulerOptions _options;
    private readonly object _upkeepSchedulesLock = new();
    private readonly ILogger<SchedulerService> _logger;
    private readonly IResetRequestSchedulerJob _resetRequestScheduler;
    private readonly IObservationCollectSchedulerJob _observationScheduler;
    private readonly IResetLogTriggerSchedulerJob _resetLogTriggerScheduler;
    private readonly IResetCronUpkeepSchedulerJob _resetCronUpkeepScheduler;
    private readonly ConcurrentDictionary<string, HashSet<string>> _upkeepSchedules = new();

    public SchedulerService(IResetRequestSchedulerJob resetRequestScheduler, ILogger<SchedulerService> logger,
        IOptionsSnapshot<SchedulerOptions> schedulerOptions, IObservationCollectSchedulerJob observationScheduler,
        IResetLogTriggerSchedulerJob resetLogTriggerScheduler, IResetCronUpkeepSchedulerJob resetCronUpkeepScheduler)
    {
        _logger = logger;
        _options = schedulerOptions.Value;
        _observationScheduler = observationScheduler;
        _resetRequestScheduler = resetRequestScheduler;
        _resetLogTriggerScheduler = resetLogTriggerScheduler;
        _resetCronUpkeepScheduler = resetCronUpkeepScheduler;
        ListenForStart();
        ListenForEnd();
    }

    public void StartScheduler(JobDto job, SchedulerType type)
    {
        JobManager.UseUtcTime();
        DateTime overTime;
        var registry = new Registry();
        var schedulerName = GenerateScheduleName(job.ChainId, job.RequestId, type);
        CancelSchedulerByName(schedulerName);

        switch (type)
        {
            case SchedulerType.ObservationCollectWaitingScheduler:
                overTime = DateTime.Now.AddMinutes(_options.ObservationCollectTimeoutWindow);
                registry.Schedule(() => _observationScheduler.Execute(job)).WithName(schedulerName).NonReentrant()
                    .ToRunOnceAt(overTime);
                break;
            case SchedulerType.CheckRequestEndScheduler:
                overTime = job.RequestReceiveTime.AddMinutes(_options.CheckRequestEndTimeoutWindow);
                registry.Schedule(() => _resetRequestScheduler.Execute(job)).WithName(schedulerName)
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

    public void StartScheduler(LogTriggerDto upkeep)
    {
        JobManager.UseUtcTime();
        var registry = new Registry();
        AddOrUpdateUpkeepSchedulers(upkeep);

        var schedulerName = GenerateScheduleName(upkeep);
        CancelSchedulerByName(schedulerName);

        var overTime = upkeep.ReceiveTime.AddMinutes(_options.CheckRequestEndTimeoutWindow);
        registry.Schedule(() => _resetLogTriggerScheduler.Execute(upkeep)).WithName(schedulerName)
            .NonReentrant().ToRunOnceAt(overTime);

        if (DateTime.UtcNow > overTime) return;
        _logger.LogDebug("[SchedulerService] Registry scheduler {name} OverTime:{overTime}", schedulerName, overTime);
        JobManager.Initialize(registry);
    }

    public void StartScheduler(RampMessageDto rampMessage)
    {
        // todo: add ramp message scheduler
    }

    public void StartCronUpkeepScheduler(JobDto job)
    {
        JobManager.UseUtcTime();
        var registry = new Registry();
        var schedulerName = GenerateScheduleName(job.ChainId, job.RequestId,
            UpkeepSchedulerType.CheckCronUpkeepEndScheduler);
        CancelSchedulerByName(schedulerName);
        var overTime = job.RequestReceiveTime.AddMinutes(_options.CheckRequestEndTimeoutWindow);
        registry.Schedule(() => _resetCronUpkeepScheduler.Execute(job)).WithName(schedulerName)
            .NonReentrant().ToRunOnceAt(overTime);

        if (DateTime.UtcNow > overTime) return;
        _logger.LogDebug("[SchedulerService] Registry scheduler {name} OverTime:{overTime}", schedulerName, overTime);
        JobManager.Initialize(registry);
    }

    private void AddOrUpdateUpkeepSchedulers(LogTriggerDto upkeep)
    {
        lock (_upkeepSchedulesLock)
        {
            var id = IdGeneratorHelper.GenerateId(upkeep.Context.ChainId, upkeep.Context.RequestId);
            var schedulerId = GenerateScheduleName(upkeep);
            if (!_upkeepSchedules.TryGetValue(id, out var scheduleSet))
            {
                _upkeepSchedules[id] = new() { schedulerId };
                return;
            }

            scheduleSet.Add(schedulerId);
            _upkeepSchedules[id] = scheduleSet;
        }
    }

    public void CancelScheduler(JobDto job, SchedulerType type)
        => CancelSchedulerByName(GenerateScheduleName(job.ChainId, job.RequestId, type));

    public void CancelLogUpkeep(LogTriggerDto upkeep)
    {
        lock (_upkeepSchedulesLock)
        {
            var id = IdGeneratorHelper.GenerateId(upkeep.Context.ChainId, upkeep.Context.RequestId);
            var schedulerId = GenerateScheduleName(upkeep);
            if (_upkeepSchedules.TryGetValue(id, out var scheduleSet))
            {
                scheduleSet.Remove(schedulerId);
                _upkeepSchedules[id] = scheduleSet;
            }

            CancelSchedulerByName(GenerateScheduleName(upkeep));
        }
    }

    public void CancelCronUpkeep(JobDto job) => CancelSchedulerByName(GenerateScheduleName(job.ChainId, job.RequestId,
        UpkeepSchedulerType.CheckCronUpkeepEndScheduler));

    public void CancelAllSchedule(JobDto job)
    {
        foreach (SchedulerType schedulerType in Enum.GetValues(typeof(SchedulerType)))
        {
            CancelSchedulerByName(GenerateScheduleName(job.ChainId, job.RequestId, schedulerType));
        }
    }

    public void CancelLogUpkeepAllSchedule(LogUpkeepInfoDto upkeep)
    {
        lock (_upkeepSchedulesLock)
        {
            var id = IdGeneratorHelper.GenerateId(upkeep.ChainId, upkeep.UpkeepId);
            if (!_upkeepSchedules.TryGetValue(id, out var scheduleSet)) return;

            scheduleSet.ForEach(CancelSchedulerByName);
            _upkeepSchedules[id] = new();
        }
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
        => IdGeneratorHelper.GenerateId(chainId, requestId, scheduleType);

    private static string GenerateScheduleName(string chainId, string requestId, UpkeepSchedulerType type)
        => IdGeneratorHelper.GenerateId(chainId, requestId, type);

    private static string GenerateScheduleName(LogTriggerDto trigger)
        => IdGeneratorHelper.GenerateId(trigger.Context.ChainId, trigger.Context.RequestId,
            trigger.TransactionEventStorageId, trigger.LogUpkeepStorageId);
}