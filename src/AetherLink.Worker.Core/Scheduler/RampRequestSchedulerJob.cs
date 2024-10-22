using System;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.Scheduler;

public interface IRampRequestSchedulerJob
{
    public Task Execute(RampMessageDto job);
    public Task Resend(RampMessageDto job);
}

public class RampRequestSchedulerJob : IRampRequestSchedulerJob, ITransientDependency
{
    private readonly IObjectMapper _objectMapper;
    private readonly SchedulerOptions _schedulerOptions;
    private readonly IRampMessageProvider _rampMessageProvider;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ILogger<ResetCronUpkeepSchedulerJob> _logger;

    public RampRequestSchedulerJob(IBackgroundJobManager backgroundJobManager, IObjectMapper objectMapper,
        ILogger<ResetCronUpkeepSchedulerJob> logger, IRampMessageProvider rampMessageProvider,
        IOptions<SchedulerOptions> schedulerOptions)
    {
        _logger = logger;
        _objectMapper = objectMapper;
        _rampMessageProvider = rampMessageProvider;
        _schedulerOptions = schedulerOptions.Value;
        _backgroundJobManager = backgroundJobManager;
    }

    public async Task Execute(RampMessageDto messageData)
    {
        _logger.LogInformation(
            $"[RampRequestSchedulerJob] Scheduler message execute. reqId {messageData.MessageId}, roundId:{messageData.RoundId}, reqState:{messageData.State}");
        messageData.RoundId++;

        var receiveTime = messageData.RequestReceiveTime;
        while (DateTime.UtcNow > receiveTime) receiveTime = receiveTime.AddMinutes(_schedulerOptions.RetryTimeOut);
        messageData.RequestReceiveTime = receiveTime;

        await _rampMessageProvider.SetAsync(messageData);

        var hangfireJobId = await _backgroundJobManager.EnqueueAsync(
            _objectMapper.Map<RampMessageDto, RampRequestStartJobArgs>(messageData));
        _logger.LogInformation(
            $"[RampRequestSchedulerJob] Message {messageData.MessageId} timeout, will starting in new round:{messageData.RoundId}, hangfireId:{hangfireJobId}");
    }

    public async Task Resend(RampMessageDto messageData)
    {
        messageData.RoundId = 0;
        messageData.State = RampRequestState.PendingResend;
        messageData.RequestReceiveTime =
            messageData.ResendTransactionBlockTime.AddMinutes(messageData.NextCommitDelayTime);
        await _rampMessageProvider.SetAsync(messageData);

        var hangfireJobId = await _backgroundJobManager.EnqueueAsync(
            _objectMapper.Map<RampMessageDto, RampRequestStartJobArgs>(messageData));
        _logger.LogInformation(
            $"[RampRequestSchedulerJob] Message {messageData.MessageId} time to resend, hangfireId:{hangfireJobId}");
    }
}