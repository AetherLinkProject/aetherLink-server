using System;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Scheduler;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.JobPipeline.CrossChain;

public class CrossChainRequestCancelJob : IAsyncBackgroundJob<CrossChainRequestCancelJobArgs>,
    ITransientDependency
{
    private readonly ISchedulerService _schedulerService;
    private readonly ILogger<CrossChainRequestCancelJob> _logger;
    private readonly ICrossChainRequestProvider _crossChainRequestProvider;

    public CrossChainRequestCancelJob(ISchedulerService schedulerService, ILogger<CrossChainRequestCancelJob> logger,
        ICrossChainRequestProvider crossChainRequestProvider)
    {
        _logger = logger;
        _schedulerService = schedulerService;
        _crossChainRequestProvider = crossChainRequestProvider;
    }

    public async Task ExecuteAsync(CrossChainRequestCancelJobArgs args)
    {
        try
        {
            var rampMessageData = await _crossChainRequestProvider.GetAsync(args.MessageId);
            if (rampMessageData == null)
            {
                _logger.LogWarning($"[CrossChainRequestCancel] {args.MessageId} not exist");
                return;
            }

            rampMessageData.State = CrossChainState.RequestCanceled;
            await _crossChainRequestProvider.SetAsync(rampMessageData);
            _schedulerService.CancelAllSchedule(rampMessageData);

            _logger.LogInformation($"[CrossChainRequestCancel] Request {args.MessageId} cancelled.");
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[CrossChainRequestCancel] {args.MessageId} cancel failed");
        }
        finally
        {
            //todo: clean up state
        }
    }
}