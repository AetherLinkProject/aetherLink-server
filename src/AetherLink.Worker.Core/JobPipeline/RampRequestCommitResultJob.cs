using System.Threading.Tasks;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Scheduler;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.JobPipeline;

public class RampRequestCommitResultJob : AsyncBackgroundJob<RampRequestCommitResultJobArgs>, ITransientDependency
{
    private readonly ISchedulerService _schedulerService;
    private readonly IRampMessageProvider _messageProvider;
    private readonly ILogger<RampRequestCommitResultJob> _logger;

    public RampRequestCommitResultJob(ILogger<RampRequestCommitResultJob> logger, IRampMessageProvider messageProvider,
        ISchedulerService schedulerService)
    {
        _logger = logger;
        _messageProvider = messageProvider;
        _schedulerService = schedulerService;
    }

    public override async Task ExecuteAsync(RampRequestCommitResultJobArgs args)
    {
        var messageId = args.MessageId;
        _logger.LogInformation($"get leader ramp commit transaction {args.TransactionId}");

        var messageData = await _messageProvider.GetAsync(messageId);
        if (messageData == null) return;

        // todo: check transaction by targetChainId

        messageData.State = RampRequestState.Committed;
        await _messageProvider.SetAsync(messageData);
        // if successful cancel scheduler else retry.
        _schedulerService.CancelScheduler(messageData);

        _logger.LogInformation($"{args.MessageId} commit successful.");
    }
}