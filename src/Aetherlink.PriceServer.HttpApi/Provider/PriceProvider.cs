using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Dtos;
using AetherlinkPriceServer.Helper;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Volo.Abp.DependencyInjection;

namespace AetherlinkPriceServer.Provider;

public interface IPriceProvider
{
    public Task UpdatePrice(string key, PriceDto data);
    public Task<PriceDto> GetPriceAsync(string key);
    public Task<List<PriceDto>> GetPriceListAsync(IEnumerable<string> keys);
}

public class PriceProvider : IPriceProvider, ITransientDependency
{
    private readonly IStorageProvider _storage;
    private readonly ILogger<PriceProvider> _logger;

    public PriceProvider(IStorageProvider storage, ILogger<PriceProvider> logger)
    {
        _logger = logger;
        _storage = storage;
    }

    public async Task UpdatePrice(string key, PriceDto data)
    {
        var newKey = GenerateKey(key);
        _logger.LogDebug($"Save {newKey} in storage");

        await _storage.SetAsync(newKey, data);
    }

    public async Task<List<PriceDto>> GetPriceListAsync(IEnumerable<string> keys)
        => await _storage.GetAsync<PriceDto>(keys.Select(k => (RedisKey)GenerateKey(k)).ToArray());

    public async Task<PriceDto> GetPriceAsync(string key) => await _storage.GetAsync<PriceDto>(GenerateKey(key));
    private string GenerateKey(string key) => IdGeneratorHelper.GenerateId(RedisConstants.PriceRedisKey, key.ToLower());
}