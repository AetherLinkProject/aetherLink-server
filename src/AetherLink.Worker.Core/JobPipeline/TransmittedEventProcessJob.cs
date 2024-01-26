using System;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Scheduler;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.JobPipeline;

public class TransmittedEventProcessJob : AsyncBackgroundJob<TransmittedEventProcessJobArgs>, ISingletonDependency
{
    private readonly IJobProvider _jobProvider;
    private readonly ISchedulerService _schedulerService;
    private readonly ILogger<TransmittedEventProcessJob> _logger;

    public TransmittedEventProcessJob(ISchedulerService schedulerService, ILogger<TransmittedEventProcessJob> logger,
        IJobProvider jobProvider)
    {
        _logger = logger;
        _jobProvider = jobProvider;
        _schedulerService = schedulerService;
    }

    public override async Task ExecuteAsync(TransmittedEventProcessJobArgs args)
    {
        var argId = IdGeneratorHelper.GenerateId(args.ChainId, args.RequestId);
        try
        {
            var job = await _jobProvider.GetAsync(args);
            if (job == null)
            {
                _logger.LogDebug("[Transmitted] {name} not need update.", argId);
                return;
            }

            _schedulerService.CancelAllSchedule(job);
            _logger.LogInformation("[Transmitted] {name} epoch:{epoch} end", argId, job.Epoch);

            job.TransactionBlockTime = args.StartTime;
            job.State = RequestState.RequestEnd;
            job.RoundId = 0;
            job.Epoch = args.Epoch;
            await _jobProvider.SetAsync(job);

            _logger.LogDebug("[Transmitted] {name} will update epoch => {epoch}, block-time => {time}", argId,
                args.Epoch, args.StartTime);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Transmitted] {name} Transmitted process failed.", argId);
        }
    }
}