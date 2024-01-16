using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Consts;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.PeerManager;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.Scheduler;

public interface ISchedulerJob
{
    Task Execute(RequestDto request, SchedulerType type);
}

public class ResetRequestSchedulerJob : ISchedulerJob, ISingletonDependency
{
    private readonly IPeerManager _peerManager;
    private readonly IObjectMapper _objectMapper;
    private readonly SchedulerOptions _schedulerOptions;
    private readonly IContractProvider _contractProvider;
    private readonly IJobRequestProvider _jobRequestProvider;
    private readonly ILogger<ResetRequestSchedulerJob> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ConcurrentDictionary<string, int> _retryCount = new();

    public ResetRequestSchedulerJob(IBackgroundJobManager backgroundJobManager, IObjectMapper objectMapper,
        ILogger<ResetRequestSchedulerJob> logger, IPeerManager peerManager, IContractProvider contractProvider,
        IJobRequestProvider jobRequestProvider, IOptionsSnapshot<SchedulerOptions> schedulerOptions)
    {
        _logger = logger;
        _peerManager = peerManager;
        _objectMapper = objectMapper;
        _contractProvider = contractProvider;
        _jobRequestProvider = jobRequestProvider;
        _schedulerOptions = schedulerOptions.Value;
        _backgroundJobManager = backgroundJobManager;
    }

    public async Task Execute(RequestDto request, SchedulerType type)
    {
        try
        {
            if (request.State == RequestState.RequestCanceled) return;

            var chainId = request.ChainId;
            _logger.LogInformation(
                "[ResetScheduler] Scheduler job execute. reqId {ReqId}, roundId:{RoundId}, reqState:{State}, type:{type}",
                request.RequestId, request.RoundId, request.State.ToString(), type.ToString());
            request.RoundId++;
            request.Retrying = true;

            _logger.LogDebug("[ResetScheduler] blockTime {time}", request.TransactionBlockTime);
            while (DateTime.UtcNow > DateTimeOffset.FromUnixTimeMilliseconds(request.TransactionBlockTime).DateTime)
            {
                request.TransactionBlockTime += _schedulerOptions.RetryTimeOut * 60 * 1000;
            }

            var key = IdGeneratorHelper.GenerateId(chainId, request.RequestId, request.Epoch);
            _retryCount.TryGetValue(key, out var times);

            _logger.LogDebug("[ResetScheduler] ReqId: {ReqId}, times: {times}, blockTime: {blockTime}",
                request.RequestId, times, request.TransactionBlockTime);

            var args = _objectMapper.Map<RequestDto, RequestStartProcessJobArgs>(request);
            _retryCount[key] = times + 1;
            if (_retryCount[key] > _schedulerOptions.RetryCount)
            {
                var newEpoch = await _contractProvider.GetLatestRoundAsync(chainId);
                if (newEpoch.Value > request.Epoch)
                {
                    _peerManager.UpdateEpoch(chainId, newEpoch.Value);
                    args.Epoch = newEpoch.Value;
                    args.RoundId = 0;
                }

                _logger.LogDebug("[ResetScheduler] Update epoch, ReqId {ReqId}, time: {time}, newEpoch{epoch}",
                    request.RequestId, _retryCount[key], newEpoch.Value);
            }

            await _jobRequestProvider.SetJobRequestAsync(request);

            var hangfireJobId = await _backgroundJobManager.EnqueueAsync(args, BackgroundJobPriority.High);
            _logger.LogInformation(
                "[ResetScheduler] Request timeout, will starting in new round. ReqId {ReqId}, roundId:{RoundId}, type:{type}, hangfireId:{hangfire}, startTime:{startTime}",
                request.RequestId, request.RoundId, type.ToString(), hangfireJobId, args.StartTime);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[ResetScheduler] Reset scheduler job failed.");
        }
    }
}