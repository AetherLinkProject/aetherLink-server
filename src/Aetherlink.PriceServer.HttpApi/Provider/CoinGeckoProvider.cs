using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Common;
using AetherlinkPriceServer.Dtos;
using CoinGecko.Interfaces;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace AetherlinkPriceServer.Provider;

public interface ICoinGeckoProvider
{
    public Task<PriceDto> GetTokenPriceAsync(string tokenPair);
    public Task<List<PriceDto>> GetTokenPricesAsync(List<string> tokenPairs);
    public Task<long> GetHistoricPriceAsync(string tokenPair, DateTime time);
}

public class CoinGeckoProvider : ICoinGeckoProvider, ITransientDependency
{
    private const string FiatSymbol = "usd";
    private readonly ICoinGeckoClient _coinGeckoClient;
    private readonly Dictionary<string, string> _tokenDict;

    public CoinGeckoProvider(ICoinGeckoClient coinGeckoClient)
    {
        _coinGeckoClient = coinGeckoClient;
        _tokenDict = CoinGeckoConstants.IdMap.ToDictionary(x => x.Value, x => x.Key);
    }

    public async Task<PriceDto> GetTokenPriceAsync(string tokenPair)
    {
        var result = await GetTokenPricesAsync(new() { tokenPair });
        return result.Count != 0 ? result.First() : null;
    }

    public async Task<List<PriceDto>> GetTokenPricesAsync(List<string> tokenPairs) =>
        (await _coinGeckoClient.SimpleClient.GetSimplePrice(
            tokenPairs.Select(GenerateCoinId).Where(c => !string.IsNullOrEmpty(c)).ToArray(), new[] { FiatSymbol }))
        .Select(kv => new PriceDto
        {
            TokenPair = $"{_tokenDict[kv.Key]}-USD",
            Price = PriceConvertHelper.ConvertPrice((double)kv.Value[FiatSymbol].Value),
            UpdateTime = DateTime.Now
        }).ToList();

    public async Task<long> GetHistoricPriceAsync(string tokenPair, DateTime time)
    {
        var coinId = GenerateCoinId(tokenPair);
        if (string.IsNullOrEmpty(coinId)) throw new UserFriendlyException("Not support token");

        var fullData = await _coinGeckoClient.CoinsClient.GetHistoryByCoinId(coinId,
            time.ToString("dd-MM-yyyy"), "false");

        return PriceConvertHelper.ConvertPrice((double)fullData.MarketData.CurrentPrice[FiatSymbol].Value);
    }

    private string GenerateCoinId(string tokenPair)
    {
        var pair = tokenPair.Split('-');
        if (pair.Length != 2 || pair[1].ToLower() != FiatSymbol) return "";

        return CoinGeckoConstants.IdMap.TryGetValue(pair[0].ToUpper(), out var coinId) ? coinId : "";
    }
}