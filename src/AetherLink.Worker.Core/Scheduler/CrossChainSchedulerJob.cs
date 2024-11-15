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

public interface ICrossChainSchedulerJob
{
    public Task Execute(CrossChainDataDto crossChainData);
    public Task Resend(CrossChainDataDto job);
}

public class CrossChainSchedulerJob : ICrossChainSchedulerJob, ITransientDependency
{
    private readonly IObjectMapper _objectMapper;
    private readonly SchedulerOptions _schedulerOptions;
    private readonly ILogger<CrossChainSchedulerJob> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ICrossChainRequestProvider _crossChainRequestProvider;

    public CrossChainSchedulerJob(IBackgroundJobManager backgroundJobManager, IObjectMapper objectMapper,
        ILogger<CrossChainSchedulerJob> logger, IOptions<SchedulerOptions> schedulerOptions,
        ICrossChainRequestProvider crossChainRequestProvider)
    {
        _logger = logger;
        _objectMapper = objectMapper;
        _schedulerOptions = schedulerOptions.Value;
        _backgroundJobManager = backgroundJobManager;
        _crossChainRequestProvider = crossChainRequestProvider;
    }

    public async Task Execute(CrossChainDataDto data)
    {
        var reportContext = data.ReportContext;
        _logger.LogInformation(
            $"[CrossChainSchedulerJob] Scheduler message execute. reqId {reportContext.MessageId}, roundId:{reportContext.RoundId}, reqState:{data.State}");
        data.ReportContext.RoundId++;

        var receiveTime = data.RequestReceiveTime;
        while (DateTime.UtcNow > receiveTime) receiveTime = receiveTime.AddMinutes(_schedulerOptions.RetryTimeOut);
        data.RequestReceiveTime = receiveTime;

        await _crossChainRequestProvider.SetAsync(data);

        var hangfireJobId = await _backgroundJobManager.EnqueueAsync(
            _objectMapper.Map<CrossChainDataDto, CrossChainRequestStartArgs>(data));
        _logger.LogInformation(
            $"[CrossChainSchedulerJob] Message {reportContext.MessageId} timeout, will starting in new round:{data.ReportContext.RoundId}, hangfireId:{hangfireJobId}");
    }

    public async Task Resend(CrossChainDataDto data)
    {
        data.ReportContext.RoundId = 0;
        data.State = CrossChainState.PendingResend;
        data.RequestReceiveTime =
            data.ResendTransactionBlockTime.AddMinutes(data.NextCommitDelayTime);
        await _crossChainRequestProvider.SetAsync(data);

        var hangfireJobId = await _backgroundJobManager.EnqueueAsync(
            _objectMapper.Map<CrossChainDataDto, CrossChainRequestStartArgs>(data));
        _logger.LogInformation(
            $"[CrossChainSchedulerJob] Message {data.ReportContext.MessageId} time to resend, hangfireId:{hangfireJobId}");
    }
}