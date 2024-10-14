using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;

namespace AetherlinkPriceServer.Provider;

public interface IStorageProvider
{
    public Task SetAsync<T>(string key, T data) where T : class;
    public Task SetAsync<T>(string key, T data, TimeSpan? expiry) where T : class;
    public Task SetAsync<T>(KeyValuePair<string, T>[] values) where T : class;
    public Task<T> GetAsync<T>(string key) where T : class, new();
    public Task<List<T>> GetAsync<T>(RedisKey[] keys) where T : class, new();
}

public class StorageProvider : AbpRedisCache, IStorageProvider, ITransientDependency
{
    private readonly ILogger<StorageProvider> _logger;
    private readonly IDistributedCacheSerializer _serializer;

    public StorageProvider(IOptions<RedisCacheOptions> optionsAccessor, ILogger<StorageProvider> logger,
        IDistributedCacheSerializer serializer) : base(optionsAccessor)
    {
        _logger = logger;
        _serializer = serializer;
    }

    public async Task SetAsync<T>(string key, T data) where T : class => await SetAsync(key, data, null);

    [ExceptionHandler(typeof(Exception), TargetType = typeof(StorageProvider),
        MethodName = nameof(HandleSetAsyncMessageException))]
    public virtual async Task SetAsync<T>(string key, T data, TimeSpan? expiry) where T : class
    {
        await ConnectAsync();

        await RedisDatabase.StringSetAsync(key, _serializer.Serialize(data), expiry);
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(StorageProvider),
        MethodName = nameof(HandleSetMessageException))]
    public virtual async Task SetAsync<T>(KeyValuePair<string, T>[] values) where T : class
    {
        await ConnectAsync();

        await RedisDatabase.StringSetAsync(values
            .Select(kv => new KeyValuePair<RedisKey, RedisValue>(kv.Key, _serializer.Serialize(kv.Value)))
            .ToArray());
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(StorageProvider),
        MethodName = nameof(HandleGetKeyAsyncException))]
    public virtual async Task<T> GetAsync<T>(string key) where T : class, new()
    {
        await ConnectAsync();

        var redisValue = await RedisDatabase.StringGetAsync(key);

        // _logger.LogDebug("[StorageProvider] {key} spec: {spec}", key, redisValue);

        return redisValue.HasValue ? _serializer.Deserialize<T>(redisValue) : null;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(StorageProvider),
        MethodName = nameof(HandleGetAsyncException))]
    public virtual async Task<List<T>> GetAsync<T>(RedisKey[] keys) where T : class, new()
    {
        await ConnectAsync();
        var result = await RedisDatabase.StringGetAsync(keys);
        return result.Where(v => v.HasValue)
            .Select(v => _serializer.Deserialize<T>(v)).ToList();
    }

    #region Exception Handing

    public async Task<FlowBehavior> HandleGetKeyAsyncException(Exception ex, string key)
    {
        _logger.LogError(ex, $"Get {key} error.");
        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = null
        };
    }

    public async Task<FlowBehavior> HandleGetAsyncException(Exception ex, RedisKey[] keys)
    {
        _logger.LogError(ex, $"Get {string.Join(",", keys)} error.");
        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = null
        };
    }

    public async Task<FlowBehavior> HandleSetMessageException(Exception ex)
    {
        _logger.LogError(ex, "Batch set to redis error.");
        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = null
        };
    }

    public async Task<FlowBehavior> HandleSetAsyncMessageException(Exception ex, string key)
    {
        _logger.LogError(ex, "Set {key} to redis error.", key);

        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = null
        };
    }

    #endregion
}