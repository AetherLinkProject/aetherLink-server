using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;
using Serilog;

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
    private readonly ILogger _logger;
    private readonly IDistributedCacheSerializer _serializer;

    public StorageProvider(IOptions<RedisCacheOptions> optionsAccessor, IDistributedCacheSerializer serializer) : base(
        optionsAccessor)
    {
        _logger = Log.ForContext<StorageProvider>();
        _serializer = serializer;
    }

    public async Task SetAsync<T>(string key, T data) where T : class => await SetAsync(key, data, null);

    public async Task SetAsync<T>(string key, T data, TimeSpan? expiry) where T : class
    {
        try
        {
            await ConnectAsync();

            await RedisDatabase.StringSetAsync(key, _serializer.Serialize(data), expiry);
        }
        catch (Exception e)
        {
            _logger.Error(e, "Set {key} to redis error.", key);
        }
    }

    public async Task SetAsync<T>(KeyValuePair<string, T>[] values) where T : class
    {
        try
        {
            await ConnectAsync();

            await RedisDatabase.StringSetAsync(values
                .Select(kv => new KeyValuePair<RedisKey, RedisValue>(kv.Key, _serializer.Serialize(kv.Value)))
                .ToArray());
        }
        catch (Exception e)
        {
            _logger.Error(e, "Batch set to redis error.");
        }
    }

    public async Task<T> GetAsync<T>(string key) where T : class, new()
    {
        try
        {
            await ConnectAsync();

            var redisValue = await RedisDatabase.StringGetAsync(key);

            // _logger.LogDebug("[StorageProvider] {key} spec: {spec}", key, redisValue);

            return redisValue.HasValue ? _serializer.Deserialize<T>(redisValue) : null;
        }
        catch (Exception e)
        {
            _logger.Error(e, $"Get {key} error.");
            return null;
        }
    }

    public async Task<List<T>> GetAsync<T>(RedisKey[] keys) where T : class, new()
    {
        try
        {
            await ConnectAsync();
            var result = await RedisDatabase.StringGetAsync(keys);
            return result.Where(v => v.HasValue)
                .Select(v => _serializer.Deserialize<T>(v)).ToList();
        }
        catch (Exception e)
        {
            _logger.Error(e, $"Get {string.Join(",", keys)} error.");
            return null;
        }
    }
}