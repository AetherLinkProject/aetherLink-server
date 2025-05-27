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

public class CrossChainCommitAcceptedJob : AsyncBackgroundJob<CrossChainCommitAcceptedJobArgs>, ITransientDependency
{
    private readonly ISchedulerService _schedulerService;
    private readonly ILogger<CrossChainCommitAcceptedJob> _logger;
    private readonly ICrossChainRequestProvider _crossChainRequestProvider;

    public CrossChainCommitAcceptedJob(ICrossChainRequestProvider crossChainRequestProvider,
        ILogger<CrossChainCommitAcceptedJob> logger, ISchedulerService schedulerService)
    {
        _logger = logger;
        _schedulerService = schedulerService;
        _crossChainRequestProvider = crossChainRequestProvider;
    }

    public override async Task ExecuteAsync(CrossChainCommitAcceptedJobArgs args)
    {
        try
        {
            var rampMessageData = await _crossChainRequestProvider.TryGetRampMessageDataAsync(args.MessageId);
            if (rampMessageData == null)
            {
                _logger.LogWarning($"[CrossChainCommitAccepted] {args.MessageId} not exist");
                return;
            }

            _schedulerService.CancelScheduler(rampMessageData);

            rampMessageData.State = CrossChainState.Committed;
            await _crossChainRequestProvider.SetAsync(rampMessageData);

            _logger.LogInformation($"[CrossChainCommitAccepted] Request {args.MessageId} commit accepted.");
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[CrossChainCommitAccepted] Get {args.MessageId} failed");
        }
    }
}