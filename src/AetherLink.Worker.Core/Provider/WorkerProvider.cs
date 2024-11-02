using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.Provider;

public interface IWorkerProvider
{
    public Task<List<OcrLogEventDto>> SearchJobsAsync(string chainId, long to, long from);
    public Task<List<RampRequestDto>> SearchRampRequestsAsync(string chainId, long to, long from);
    public Task<List<TransmittedDto>> SearchTransmittedAsync(string chainId, long to, long from);
    public Task<List<RequestCancelledDto>> SearchRequestCanceledAsync(string chainId, long to, long from);
    public Task<List<RampRequestCancelledDto>> SearchRampRequestCanceledAsync(string chainId, long to, long from);
    public Task HandleJobAsync(OcrLogEventDto logEvent);
    public Task HandleRampRequestAsync(RampRequestDto rampRequest);
    public Task HandleTransmittedLogEventAsync(TransmittedDto transmitted);
    public Task HandleRequestCancelledLogEventAsync(RequestCancelledDto requestCancelled);
    public Task HandleRampRequestCancelledLogEventAsync(RampRequestCancelledDto rampCancelled);
    public Task<long> GetStartHeightAsync(string chainId);
    public Task<long> GetUnconfirmedStartHeightAsync(string chainId);
    public Task SetLatestSearchHeightAsync(string chainId, long searchHeight);
    public Task SetLatestUnconfirmedHeightAsync(string chainId, long unconfirmedHeight);
}

public class WorkerProvider : AbpRedisCache, IWorkerProvider, ISingletonDependency
{
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<WorkerProvider> _logger;
    private readonly IStorageProvider _storageProvider;
    private readonly IAeFinderProvider _aeFinderProvider;
    private readonly Dictionary<int, IRequestJob> _requestJob;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public WorkerProvider(IBackgroundJobManager backgroundJobManager, ILogger<WorkerProvider> logger,
        IObjectMapper objectMapper, AeFinderProvider aeFinderProvider, IStorageProvider storageProvider,
        IOptionsSnapshot<RedisCacheOptions> optionsAccessor, IEnumerable<IRequestJob> requestJobs) : base(
        optionsAccessor)
    {
        _logger = logger;
        _objectMapper = objectMapper;
        _storageProvider = storageProvider;
        _aeFinderProvider = aeFinderProvider;
        _backgroundJobManager = backgroundJobManager;
        _requestJob = requestJobs.ToDictionary(x => x.RequestTypeIndex, y => y);
    }

    public async Task<List<OcrLogEventDto>> SearchJobsAsync(string chainId, long to, long from)
        => await _aeFinderProvider.SubscribeLogsAsync(chainId, to, from);

    public async Task<List<RampRequestDto>> SearchRampRequestsAsync(string chainId, long to, long from)
        => await _aeFinderProvider.SubscribeRampRequestsAsync(chainId, to, from);

    public async Task<List<TransmittedDto>> SearchTransmittedAsync(string chainId, long to, long from)
        => await _aeFinderProvider.SubscribeTransmittedAsync(chainId, to, from);

    public async Task<List<RequestCancelledDto>> SearchRequestCanceledAsync(string chainId, long to, long from)
        => await _aeFinderProvider.SubscribeRequestCancelledAsync(chainId, to, from);

    public async Task<List<RampRequestCancelledDto>> SearchRampRequestCanceledAsync(string chainId, long to, long from)
        => await _aeFinderProvider.SubscribeRampRequestCancelledAsync(chainId, to, from);

    public async Task HandleJobAsync(OcrLogEventDto logEvent)
    {
        if (!_requestJob.TryGetValue(logEvent.RequestTypeIndex, out var request))
        {
            _logger.LogError("unknown job type: {type}", logEvent.RequestTypeIndex);
            return;
        }

        await request.EnqueueAsync(logEvent);
    }

    public async Task HandleRampRequestAsync(RampRequestDto rampRequest)
        => await _backgroundJobManager.EnqueueAsync(
            _objectMapper.Map<RampRequestDto, RampRequestStartJobArgs>(rampRequest));

    public async Task<long> GetStartHeightAsync(string chainId)
    {
        var latestBlockHeight = await _storageProvider.GetAsync<SearchHeightDto>(GetSearchHeightRedisKey(chainId));
        return latestBlockHeight?.BlockHeight ?? 0;
    }

    public async Task<long> GetUnconfirmedStartHeightAsync(string chainId) =>
        (await _storageProvider.GetAsync<SearchHeightDto>(GetUnconfirmedHeightRedisKey(chainId)))?.BlockHeight ?? 0;

    public async Task SetLatestSearchHeightAsync(string chainId, long searchHeight)
        => await _storageProvider.SetAsync(GetSearchHeightRedisKey(chainId),
            new SearchHeightDto { BlockHeight = searchHeight });

    public async Task SetLatestUnconfirmedHeightAsync(string chainId, long unconfirmedHeight)
        => await _storageProvider.SetAsync(GetUnconfirmedHeightRedisKey(chainId),
            new SearchHeightDto { BlockHeight = unconfirmedHeight });

    public async Task HandleTransmittedLogEventAsync(TransmittedDto transmitted)
        => await _backgroundJobManager.EnqueueAsync(
            _objectMapper.Map<TransmittedDto, TransmittedEventProcessJobArgs>(transmitted));

    public async Task HandleRequestCancelledLogEventAsync(RequestCancelledDto requestCancelled)
        => await _backgroundJobManager.EnqueueAsync(
            _objectMapper.Map<RequestCancelledDto, RequestCancelProcessJobArgs>(requestCancelled));

    public async Task HandleRampRequestCancelledLogEventAsync(RampRequestCancelledDto rampCancelled)
        => await _backgroundJobManager.EnqueueAsync(
            _objectMapper.Map<RampRequestCancelledDto, RampRequestCancelProcessJobArgs>(rampCancelled));

    private static string GetSearchHeightRedisKey(string chainId)
        => IdGeneratorHelper.GenerateId(RedisKeyConstants.SearchHeightKey, chainId);

    private static string GetUnconfirmedHeightRedisKey(string chainId)
        => IdGeneratorHelper.GenerateId(RedisKeyConstants.UnconfirmedSearchHeightKey, chainId);
}