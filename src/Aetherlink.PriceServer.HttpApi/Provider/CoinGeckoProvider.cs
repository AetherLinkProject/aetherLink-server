using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Common;
using AetherlinkPriceServer.Dtos;
using AetherlinkPriceServer.Reporter;
using CoinGecko.Interfaces;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace AetherlinkPriceServer.Provider;

public interface ICoinGeckoProvider
{
    public Task<List<PriceDto>> GetTokenPricesAsync(List<string> tokenPairs);
    public Task<long> GetHistoricPriceAsync(string tokenPair, DateTime time);
}

public class CoinGeckoProvider : ICoinGeckoProvider, ITransientDependency
{
    private const string FiatSymbol = "usd";
    private readonly IPriceCollectReporter _reporter;
    private readonly ICoinGeckoClient _coinGeckoClient;
    private readonly Dictionary<string, string> _tokenDict;

    public CoinGeckoProvider(ICoinGeckoClient coinGeckoClient, IPriceCollectReporter reporter)
    {
        _reporter = reporter;
        _coinGeckoClient = coinGeckoClient;
        _tokenDict = CoinGeckoConstants.IdMap.ToDictionary(x => x.Value, x => x.Key);
    }

    public async Task<List<PriceDto>> GetTokenPricesAsync(List<string> tokenPairs)
    {
        var timer = _reporter.GetPriceCollectLatencyTimer(SourceType.CoinGecko, string.Join(",", tokenPairs));
        try
        {
            var priceList = await _coinGeckoClient.SimpleClient.GetSimplePrice(
                tokenPairs.Select(GenerateCoinId).Where(c => !string.IsNullOrEmpty(c)).ToArray(), new[] { FiatSymbol });

            return priceList.Select(kv =>
            {
                var tempPair = $"{_tokenDict[kv.Key]}-USD";
                var tempPrice = PriceConvertHelper.ConvertPrice((double)kv.Value[FiatSymbol].Value);

                _reporter.RecordPriceCollected(SourceType.CoinGecko, tempPair, tempPrice);

                return new PriceDto
                {
                    TokenPair = tempPair,
                    Price = tempPrice,
                    UpdateTime = DateTime.Now
                };
            }).ToList();
        }
        finally
        {
            timer.ObserveDuration();
        }
    }

    public async Task<long> GetHistoricPriceAsync(string tokenPair, DateTime time)
    {
        var timer = _reporter.GetPriceCollectLatencyTimer(SourceType.CoinGecko, tokenPair);
        try
        {
            var coinId = GenerateCoinId(tokenPair);
            if (string.IsNullOrEmpty(coinId)) throw new UserFriendlyException("Not support token");

            var fullData = await _coinGeckoClient.CoinsClient.GetHistoryByCoinId(coinId,
                time.ToString("dd-MM-yyyy"), "false");

            return PriceConvertHelper.ConvertPrice((double)fullData.MarketData.CurrentPrice[FiatSymbol].Value);
        }
        finally
        {
            timer.ObserveDuration();
        }
    }

    private string GenerateCoinId(string tokenPair)
    {
        var pair = tokenPair.Split('-');
        if (pair.Length != 2 || pair[1].ToLower() != FiatSymbol) return "";

        return CoinGeckoConstants.IdMap.TryGetValue(pair[0].ToUpper(), out var coinId) ? coinId : "";
    }
}