using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Common;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Dtos;
using AetherlinkPriceServer.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUglify.Helpers;
using StackExchange.Redis;
using Volo.Abp.DependencyInjection;

namespace AetherlinkPriceServer.Provider;

public interface IPriceProvider
{
    public Task UpdatePricesAsync(SourceType source, KeyValuePair<string, PriceDto>[] tokenPairs);
    public Task UpdateHourlyPriceAsync(DateTime time, PriceDto data);
    
    public Task<PriceDto> GetPriceAsync(string tokenPair, SourceType source = SourceType.None);
    public Task<List<PriceDto>> GetPriceListAsync(List<string> tokenPairs);
    public Task<List<PriceDto>> GetPriceListAsync(SourceType source, List<string> tokenPairs);
    public Task<List<PriceDto>> GetAllSourcePricesAsync(string tokenPair);
    public Task<List<PriceDto>> GetHourlyPriceAsync(string tokenPair);
}

public class PriceProvider : IPriceProvider, ITransientDependency
{
    private readonly IStorageProvider _storage;
    private readonly ILogger<PriceProvider> _logger;
    private readonly TokenPriceSourceOptions _sourceOptions;

    public PriceProvider(IStorageProvider storage, ILogger<PriceProvider> logger,
        IOptionsSnapshot<TokenPriceSourceOptions> sourceOptions)
    {
        _logger = logger;
        _storage = storage;
        _sourceOptions = sourceOptions.Value;
    }

    public async Task UpdateHourlyPriceAsync(DateTime time, PriceDto data)
    {
        var key = GenerateKey(time, data.TokenPair);
        _logger.LogInformation($"Hourly price updated: {key}");
        await _storage.SetAsync(key, data, TimeSpan.FromHours(24));
    }

    public async Task UpdatePricesAsync(SourceType source, KeyValuePair<string, PriceDto>[] tokenPairs)
    {
        _logger.LogDebug($"Start save {source} price list in storage.");


        var datasList = new List<KeyValuePair<string, PriceDto>>();

        tokenPairs.Where(t => !string.IsNullOrEmpty(t.Key)).ForEach(t =>
        {
            datasList.Add(new KeyValuePair<string, PriceDto>(GenerateKey(t.Key), t.Value));
            datasList.Add(new KeyValuePair<string, PriceDto>(GenerateKey(source, t.Key), t.Value));
        });

        await _storage.SetAsync(datasList.ToArray());
    }

    public async Task<List<PriceDto>> GetPriceListAsync(List<string> tokenPairs) =>
        await _storage.GetAsync<PriceDto>(tokenPairs.Select(k => (RedisKey)GenerateKey(k)).ToArray());

    public async Task<List<PriceDto>> GetPriceListAsync(SourceType source, List<string> tokenPairs) =>
        await _storage.GetAsync<PriceDto>(tokenPairs.Select(k => (RedisKey)GenerateKey(source, k)).ToArray());

    public async Task<List<PriceDto>> GetAllSourcePricesAsync(string tokenPair) => await _storage.GetAsync<PriceDto>(
        _sourceOptions.Sources.Select(t => (RedisKey)GenerateKey(t.Key, tokenPair)).ToArray());

    public async Task<PriceDto> GetPriceAsync(string tokenPair, SourceType source) => source == SourceType.None
        ? await _storage.GetAsync<PriceDto>(GenerateKey(tokenPair))
        : await _storage.GetAsync<PriceDto>(GenerateKey(source, tokenPair));

    public async Task<List<PriceDto>> GetHourlyPriceAsync(string tokenPair) =>
        await _storage.GetAsync<PriceDto>(Enumerable.Range(0, 24)
            .Select(i => (RedisKey)GenerateKey(DateTime.Now.AddHours(-23 + i), tokenPair)).ToArray());

    private string GenerateKey(string key) => IdGeneratorHelper.GenerateId(RedisConstants.PriceRedisKey, key.ToLower());

    private string GenerateKey(DateTime time, string key) =>
        IdGeneratorHelper.GenerateId(RedisConstants.PriceRedisKey, key.ToLower(), time.ToString("yyyyMMddHH"));

    private string GenerateKey(object source, string key) =>
        IdGeneratorHelper.GenerateId(RedisConstants.PriceRedisKey, source.ToString().ToLower(), key.ToLower());
}