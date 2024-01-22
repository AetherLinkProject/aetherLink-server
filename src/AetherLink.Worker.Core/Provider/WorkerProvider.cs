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
    public Task<List<TransmittedDto>> SearchTransmittedAsync(string chainId, long to, long from);
    public Task<List<RequestCancelledDto>> SearchRequestCanceledAsync(string chainId, long to, long from);
    public Task<long> GetBlockLatestHeightAsync(string chainId);
    public Task HandleJobAsync(OcrLogEventDto logEvent);
    public Task HandleTransmittedLogEventAsync(TransmittedDto transmitted);
    public Task HandleRequestCancelledLogEventAsync(RequestCancelledDto requestCancelled);
    public Task<long> GetStartHeightAsync(string chainId);
    public Task SetLatestSearchHeightAsync(string chainId, long searchHeight);
}

public class WorkerProvider : AbpRedisCache, IWorkerProvider, ISingletonDependency
{
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<WorkerProvider> _logger;
    private readonly IIndexerProvider _indexerProvider;
    private readonly IStorageProvider _storageProvider;
    private readonly IContractProvider _contractProvider;
    private readonly Dictionary<int, IRequestJob> _requestJob;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public WorkerProvider(IBackgroundJobManager backgroundJobManager, ILogger<WorkerProvider> logger,
        IObjectMapper objectMapper, IIndexerProvider indexerProvider, IContractProvider contractProvider,
        IOptionsSnapshot<RedisCacheOptions> optionsAccessor, IEnumerable<IRequestJob> requestJobs,
        IStorageProvider storageProvider) : base(optionsAccessor)
    {
        _logger = logger;
        _objectMapper = objectMapper;
        _storageProvider = storageProvider;
        _indexerProvider = indexerProvider;
        _contractProvider = contractProvider;
        _backgroundJobManager = backgroundJobManager;
        _requestJob = requestJobs.ToDictionary(x => x.RequestTypeIndex, y => y);
    }

    public async Task<long> GetBlockLatestHeightAsync(string chainId)
    {
        var currentBlockHeight = await _indexerProvider.GetIndexBlockHeightAsync(chainId);
        if (currentBlockHeight > 0) return currentBlockHeight;
        return await _contractProvider.GetBlockLatestHeightAsync(chainId);
    }

    public async Task<List<OcrLogEventDto>> SearchJobsAsync(string chainId, long to, long from)
        => await _indexerProvider.SubscribeLogsAsync(chainId, to, from);

    public async Task<List<TransmittedDto>> SearchTransmittedAsync(string chainId, long to, long from)
        => await _indexerProvider.SubscribeTransmittedAsync(chainId, to, from);

    public async Task<List<RequestCancelledDto>> SearchRequestCanceledAsync(string chainId, long to, long from)
        => await _indexerProvider.SubscribeRequestCancelledAsync(chainId, to, from);

    public async Task HandleJobAsync(OcrLogEventDto logEvent)
    {
        if (!_requestJob.TryGetValue(logEvent.RequestTypeIndex, out var request))
        {
            _logger.LogWarning("unknown job type: {type}", logEvent.RequestTypeIndex);
            return;
        }

        await request.EnqueueAsync(logEvent);
    }

    public async Task<long> GetStartHeightAsync(string chainId)
    {
        var latestBlockHeight = await _storageProvider.GetAsync<SearchHeightDto>(GetSearchHeightRedisKey(chainId));
        return latestBlockHeight?.BlockHeight ?? 0;
    }

    public async Task SetLatestSearchHeightAsync(string chainId, long searchHeight)
        => await _storageProvider.SetAsync(GetSearchHeightRedisKey(chainId),
            new SearchHeightDto { BlockHeight = searchHeight });

    public async Task HandleTransmittedLogEventAsync(TransmittedDto transmitted)
    {
        await _backgroundJobManager.EnqueueAsync(
            _objectMapper.Map<TransmittedDto, TransmittedEventProcessJobArgs>(transmitted));
    }

    public async Task HandleRequestCancelledLogEventAsync(RequestCancelledDto requestCancelled)
    {
        await _backgroundJobManager.EnqueueAsync(
            _objectMapper.Map<RequestCancelledDto, RequestCancelProcessJobArgs>(requestCancelled));
    }

    private static string GetSearchHeightRedisKey(string chainId)
        => IdGeneratorHelper.GenerateId(RedisKeyConstants.SearchHeightKey, chainId);
}