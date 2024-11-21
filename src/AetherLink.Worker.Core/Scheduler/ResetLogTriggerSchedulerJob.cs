using System;
using System.Threading.Tasks;
using AetherLink.Indexer.Dtos;
using AetherLink.Worker.Core.Automation.Args;
using AetherLink.Worker.Core.Automation.Providers;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Scheduler;

public interface IResetLogTriggerSchedulerJob
{
    public Task Execute(LogTriggerDto trigger);
}

public class ResetLogTriggerSchedulerJob : IResetLogTriggerSchedulerJob, ITransientDependency
{
    private readonly IStorageProvider _storageProvider;
    private readonly SchedulerOptions _schedulerOptions;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ILogger<ResetLogTriggerSchedulerJob> _logger;

    public ResetLogTriggerSchedulerJob(IBackgroundJobManager backgroundJobManager,
        ILogger<ResetLogTriggerSchedulerJob> logger, IOptionsSnapshot<SchedulerOptions> schedulerOptions,
        IStorageProvider storageProvider)
    {
        _logger = logger;
        _storageProvider = storageProvider;
        _schedulerOptions = schedulerOptions.Value;
        _backgroundJobManager = backgroundJobManager;
    }

    public async Task Execute(LogTriggerDto trigger)
    {
        try
        {
            if (trigger.State == RequestState.RequestCanceled) return;
            var eventStorageId = trigger.TransactionEventStorageId;
            var logUpkeepStorageId = trigger.LogUpkeepStorageId;

            _logger.LogInformation(
                $"[ResetScheduler] Scheduler trigger execute. Event {eventStorageId}, Upkeep {logUpkeepStorageId} State:{trigger.State}");
            trigger.Context.RoundId++;

            var transactionEvent = await _storageProvider.GetAsync<TransactionEventDto>(eventStorageId);
            var startTime = transactionEvent.StartTime;
            while (DateTime.UtcNow > DateTimeOffset.FromUnixTimeMilliseconds(startTime).DateTime)
            {
                startTime += _schedulerOptions.RetryTimeOut * 60 * 1000;
            }

            _logger.LogDebug($"[ResetScheduler] blockTime {startTime}");

            trigger.ReceiveTime = DateTimeOffset.FromUnixTimeMilliseconds(startTime).DateTime;
            await _storageProvider.SetAsync(AutomationHelper.GenerateLogTriggerKey(eventStorageId, logUpkeepStorageId),
                trigger);

            var hangfireJobId = await _backgroundJobManager.EnqueueAsync(
                new AutomationLogTriggerArgs
                {
                    Context = trigger.Context,
                    StartTime = startTime,
                    TransactionEventStorageId = eventStorageId,
                    LogUpkeepStorageId = logUpkeepStorageId
                }, BackgroundJobPriority.High);

            _logger.LogInformation(
                $"[ResetScheduler] Event {trigger.TransactionEventStorageId}, Upkeep {logUpkeepStorageId} timeout, will starting in new round:{trigger.Context.RoundId}, hangfireId:{hangfireJobId}");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[ResetScheduler] Reset scheduler trigger failed.");
        }
    }
}