using System;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Scheduler;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.JobPipeline;

public class RampRequestCancelProcessJob : AsyncBackgroundJob<RampRequestCancelProcessJobArgs>, ITransientDependency
{
    private readonly ISchedulerService _schedulerService;
    private readonly IRampMessageProvider _rampMessageProvider;
    private readonly ILogger<RampRequestCancelProcessJob> _logger;

    public RampRequestCancelProcessJob(IRampMessageProvider rampMessageProvider,
        ILogger<RampRequestCancelProcessJob> logger, ISchedulerService schedulerService)
    {
        _logger = logger;
        _schedulerService = schedulerService;
        _rampMessageProvider = rampMessageProvider;
    }

    public override async Task ExecuteAsync(RampRequestCancelProcessJobArgs args)
    {
        try
        {
            var rampMessageData = await _rampMessageProvider.GetAsync(args.MessageId);
            if (rampMessageData == null)
            {
                _logger.LogInformation($"[RampRequestCancelProcess] {args.MessageId} not exist");
                return;
            }

            rampMessageData.State = RampRequestState.RequestCanceled;
            await _rampMessageProvider.SetAsync(rampMessageData);
            _schedulerService.CancelScheduler(rampMessageData);
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[RampRequestCancelProcess] {args.MessageId} cancel failed");
        }
        finally
        {
            //todo: clean up state
        }
    }
}