using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Constants;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IStorageProvider
{
    public Task SetAsync<T>(string key, T data);
    public Task SetAsync<T>(string key, T data, TimeSpan? expiry);
    public Task<T> GetAsync<T>(string key) where T : class, new();
    public Task<Dictionary<string, T>> GetAsync<T>(List<string> keys) where T : class, new();
    public Task<IEnumerable<T>> GetFilteredAsync<T>(string prefix, Func<T, bool> filter) where T : class, new();
    public Task RemoveAsync(string key);
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

    public async Task SetAsync<T>(string key, T data) => await SetAsync(key, data, null);

    public async Task SetAsync<T>(string key, T data, TimeSpan? expiry)
    {
        try
        {
            await ConnectAsync();

            await RedisDatabase.StringSetAsync(key, _serializer.Serialize(data), expiry);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Set {key} to redis error.", key);
        }
    }

    public async Task<T> GetAsync<T>(string key) where T : class, new()
    {
        try
        {
            await ConnectAsync();

            var redisValue = await RedisDatabase.StringGetAsync(key);

            _logger.LogDebug("[StorageProvider] {key} spec: {spec}", key, redisValue);

            return redisValue.HasValue ? _serializer.Deserialize<T>(redisValue) : null;
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Get {key} error.");
            return null;
        }
    }

    public async Task<IEnumerable<T>> GetFilteredAsync<T>(string prefix, Func<T, bool> filter) where T : class, new()
    {
        try
        {
            await ConnectAsync();
            var result = new List<T>();
            var cursor = "0";
            do
            {
                var scanResult = await RedisDatabase.ExecuteAsync(
                    RedisNetworkConstants.ScanCommand,
                    cursor,
                    "MATCH",
                    prefix + "*",
                    "COUNT",
                    RedisNetworkConstants.DefaultScanStep
                );

                var resultArray = (RedisResult[])scanResult;
                cursor = (string)resultArray[0];

                var keys = ((RedisResult[])resultArray[1]).Select(x => x.ToString()).ToArray();
                if (keys.Length > 0)
                {
                    var values = await RedisDatabase.StringGetAsync(keys.Select(k => (RedisKey)k).ToArray());
                    for (var i = 0; i < keys.Length; i++)
                    {
                        var value = values[i];
                        if (!value.HasValue) continue;
                        var obj = _serializer.Deserialize<T>(value);
                        if (obj != null && filter(obj)) result.Add(obj);
                    }
                }

                await Task.Delay(RedisNetworkConstants.DefaultScanDelayTime);
            } while (cursor != "0");

            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to fetch and filter keys with prefix: {prefix}");
            return Enumerable.Empty<T>();
        }
    }

    public async Task<Dictionary<string, T>> GetAsync<T>(List<string> keys) where T : class, new()
    {
        try
        {
            await ConnectAsync();

            var result = await RedisDatabase.StringGetAsync(keys.Select(k => (RedisKey)k).ToArray());

            return keys.Zip(result, (k, v) => new { k, v })
                .ToDictionary(x => x.k, x => x.v.HasValue ? _serializer.Deserialize<T>(x.v) : null);
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Get {string.Join(",", keys)} error.");
            return null;
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await ConnectAsync();

            await RedisDatabase.KeyDeleteAsync(key);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Set {key} to redis error.", key);
        }
    }
}