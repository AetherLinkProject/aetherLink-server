using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Consts;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.Provider;

public interface IWorkerProvider
{
    public Task<List<OcrLogEventDto>> SearchJobsAsync(string chainId, long to, long from);
    public Task<long> GetBlockLatestHeightAsync(string chainId);
    public Task HandleJobAsync(OcrLogEventDto logEvent);
    public Task<long> GetStartHeightAsync(string chainId);
    public Task SetLatestSearchHeightAsync(string chainId, long searchHeight);
}

public class WorkerProvider : AbpRedisCache, IWorkerProvider, ISingletonDependency
{
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<WorkerProvider> _logger;
    private readonly IIndexerProvider _indexerProvider;
    private readonly IContractProvider _contractProvider;
    private readonly IDistributedCacheSerializer _serializer;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public WorkerProvider(IBackgroundJobManager backgroundJobManager, ILogger<WorkerProvider> logger,
        IObjectMapper objectMapper, IIndexerProvider indexerProvider, IContractProvider contractProvider,
        IOptionsSnapshot<RedisCacheOptions> optionsAccessor, IDistributedCacheSerializer serializer) : base(
        optionsAccessor)
    {
        _logger = logger;
        _serializer = serializer;
        _objectMapper = objectMapper;
        _indexerProvider = indexerProvider;
        _contractProvider = contractProvider;
        _backgroundJobManager = backgroundJobManager;
    }

    public async Task<long> GetBlockLatestHeightAsync(string chainId)
    {
        var currentBlockHeight = await _indexerProvider.GetIndexBlockHeightAsync(chainId);
        if (currentBlockHeight > 0) return currentBlockHeight;
        return await _contractProvider.GetBlockLatestHeightAsync(chainId);
    }

    public async Task<List<OcrLogEventDto>> SearchJobsAsync(string chainId, long to, long from)
    {
        return await _indexerProvider.SubscribeLogsAsync(chainId, to, from);
    }

    public async Task HandleJobAsync(OcrLogEventDto logEvent)
    {
        switch (logEvent.RequestTypeIndex)
        {
            case RequestTypeConst.Datafeeds:
                await _backgroundJobManager.EnqueueAsync(
                    _objectMapper.Map<OcrLogEventDto, DataFeedsProcessJobArgs>(logEvent));
                break;
            case RequestTypeConst.Vrf:
                await _backgroundJobManager.EnqueueAsync(
                    _objectMapper.Map<OcrLogEventDto, VRFJobArgs>(logEvent));
                break;
            case RequestTypeConst.Transmitted:
                await _backgroundJobManager.EnqueueAsync(
                    _objectMapper.Map<OcrLogEventDto, TransmittedProcessJobArgs>(logEvent));
                break;
            case RequestTypeConst.RequestedCancel:
                await _backgroundJobManager.EnqueueAsync(
                    _objectMapper.Map<OcrLogEventDto, RequestCancelProcessJobArgs>(logEvent));
                break;
            default:
                _logger.LogWarning("unknown job type: {type}", logEvent.RequestTypeIndex);
                return;
        }
    }

    public async Task<long> GetStartHeightAsync(string chainId)
    {
        await ConnectAsync();
        var searchHeightKey = IdGeneratorHelper.GenerateId(RedisKeyConst.SearchHeightKey, chainId);
        var height = await RedisDatabase.StringGetAsync(searchHeightKey);
        if (!height.HasValue)
        {
            _logger.LogWarning("Latest search height is not set.");
            return 0;
        }

        return _serializer.Deserialize<long>(height);
    }

    public async Task SetLatestSearchHeightAsync(string chainId, long searchHeight)
    {
        try
        {
            await ConnectAsync();
            await RedisDatabase.StringSetAsync(IdGeneratorHelper.GenerateId(RedisKeyConst.SearchHeightKey, chainId),
                searchHeight);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Set latest search height to redis error.");
        }
    }
}