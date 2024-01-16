using System;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Consts;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using Volo.Abp.DependencyInjection;
using FluentScheduler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AetherLink.Worker.Core.Scheduler;

public interface ISchedulerService
{
    void StartScheduler(RequestDto request, SchedulerType type);
    void CancelScheduler(RequestDto request, SchedulerType type);
    public DateTime UpdateBlockTime(DateTime blockStartTime);
}

public class SchedulerService : ISchedulerService, ISingletonDependency
{
    private readonly ISchedulerJob _schedulerJob;
    private readonly ILogger<SchedulerService> _logger;
    private readonly SchedulerOptions _options;

    public SchedulerService(ISchedulerJob schedulerJob, ILogger<SchedulerService> logger,
        IOptionsSnapshot<SchedulerOptions> schedulerOptions)
    {
        _logger = logger;
        _schedulerJob = schedulerJob;
        _options = schedulerOptions.Value;
        ListenForStart();
        ListenForEnd();
    }

    public void StartScheduler(RequestDto request, SchedulerType type)
    {
        JobManager.UseUtcTime();
        var registry = new Registry();
        var schedulerName = GenerateScheduleName(request.ChainId, request.RequestId, request.Epoch, type);
        _logger.LogDebug("[SchedulerService] New scheduler {name}.", schedulerName);

        var overTime = GetOverTime(request, type);

        _logger.LogDebug("[SchedulerService] {name} OverTime:{overTime}", schedulerName, overTime);

        if (DateTime.UtcNow > overTime) return;

        CancelScheduler(request, type);

        registry.Schedule(() =>
            {
                // cancel small window need cancel bigger window with same time.
                if (type != SchedulerType.CheckRequestEndScheduler)
                {
                    _logger.LogInformation("[SchedulerService] {type} timeout, cancel all scheduler with the same time",
                        type.ToString());
                    foreach (SchedulerType schedulerType in Enum.GetValues(typeof(SchedulerType)))
                    {
                        CancelScheduler(request, type);
                    }
                }

                _schedulerJob.Execute(request, type);
            }).WithName(schedulerName).NonReentrant()
            .ToRunOnceAt(overTime);
        JobManager.Initialize(registry);
    }

    public void CancelScheduler(RequestDto request, SchedulerType type)
    {
        var schedulerName = GenerateScheduleName(request.ChainId, request.RequestId, request.Epoch, type);
        var scheduler = JobManager.GetSchedule(schedulerName);
        if (scheduler == null) return;

        _logger.LogInformation("[SchedulerService] Ready to cancel scheduler: {Name}", schedulerName);
        scheduler.Disable();
        JobManager.RemoveJob(schedulerName);
    }

    private DateTime GetOverTime(RequestDto request, SchedulerType type)
    {
        return type switch
        {
            SchedulerType.CheckRequestReceiveScheduler => request.RequestReceiveTime.AddMilliseconds(
                _options.CheckRequestReceiveTimeOut * 60 * 1000),
            SchedulerType.CheckObservationResultCommitScheduler => request.RequestStartTime.AddMilliseconds(
                _options.CheckObservationResultCommitTimeOut * 60 * 1000),
            SchedulerType.CheckReportReceiveScheduler => request.ObservationResultCommitTime.AddMilliseconds(
                _options.CheckReportReceiveTimeOut * 60 * 1000),
            SchedulerType.CheckReportCommitScheduler => request.ReportSendTime.AddMilliseconds(
                _options.CheckReportCommitTimeOut * 60 * 1000),
            SchedulerType.CheckTransmitScheduler => request.ReportSignTime.AddMilliseconds(
                _options.CheckTransmitTimeOut * 60 * 1000),
            SchedulerType.CheckRequestEndScheduler => request.RequestReceiveTime.AddMilliseconds(
                _options.CheckRequestEndTimeOut * 60 * 1000),
            _ => DateTime.MinValue
        };
    }

    private void ListenForStart()
    {
        JobManager.JobStart += (info) => _logger.LogInformation("{Name}: started", info.Name);
    }

    private void ListenForEnd()
    {
        JobManager.JobEnd += (info) => _logger.LogInformation("{Name}: ended", info.Name);
    }

    private static string GenerateScheduleName(string chainId, string requestId, long epoch, SchedulerType scheduleType)
    {
        return IdGeneratorHelper.GenerateId(chainId, requestId, epoch, scheduleType);
    }

    public DateTime UpdateBlockTime(DateTime blockStartTime)
    {
        var nowTime = DateTime.Now;
        while (true)
        {
            // block += 30 < nowTime, need add 30min continue
            var temp = blockStartTime.AddMilliseconds(_options.CheckRequestEndTimeOut * 60 * 1000);
            if (temp < nowTime)
            {
                blockStartTime = temp;
                continue;
            }

            // block += 10 < nowTime, need add 10min continue
            temp = blockStartTime.AddMilliseconds(_options.CheckRequestReceiveTimeOut * 60 * 1000);
            if (temp < nowTime)
            {
                blockStartTime = temp;
                continue;
            }

            // block += 5 > nowTime, no need add, return
            temp = blockStartTime.AddMilliseconds(_options.CheckTransmitTimeOut * 60 * 1000);
            if (temp > nowTime)
            {
                return blockStartTime;
            }

            //  nowTime < block+5 < nowTime+10, nowTime+10 is RequestReceive time window
            blockStartTime = temp;
        }
    }
}