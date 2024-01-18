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
    private readonly IRequestProvider _requestProvider;
    private readonly ISchedulerService _schedulerService;
    private readonly ILogger<TransmittedEventProcessJob> _logger;

    public TransmittedEventProcessJob(ISchedulerService schedulerService, ILogger<TransmittedEventProcessJob> logger,
        IRequestProvider requestProvider)
    {
        _logger = logger;
        _requestProvider = requestProvider;
        _schedulerService = schedulerService;
    }

    public override async Task ExecuteAsync(TransmittedEventProcessJobArgs args)
    {
        var argId = IdGeneratorHelper.GenerateId(args.ChainId, args.RequestId);
        try
        {
            var request = await _requestProvider.GetAsync(args);
            if (request == null)
            {
                _logger.LogWarning("[Transmitted] {name} not need update.", argId);
                return;
            }

            _schedulerService.CancelAllSchedule(request);
            _logger.LogInformation("[Transmitted] {name} epoch:{epoch} end", argId, request.Epoch);

            request.TransactionBlockTime = args.StartTime;
            request.State = RequestState.RequestEnd;
            request.RoundId = 0;
            request.Epoch = args.Epoch;
            await _requestProvider.SetAsync(request);

            _logger.LogDebug("[Transmitted] {name} will update epoch => {epoch}, block-time => {time}", argId,
                args.Epoch, args.StartTime);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Transmitted] {name} Transmitted process failed.", argId);
        }
    }
}