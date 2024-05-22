using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Common;
using CoinGecko.Interfaces;
using AetherlinkPriceServer.Options;
using AetherlinkPriceServer.Provider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherlinkPriceServer.Worker;

public class CoinGeckoTokenPriceSearchWorker : TokenPriceSearchWorkerBase
{
    private readonly string[] _coinIds;
    private const string FiatSymbol = "usd";
    private readonly ICoinGeckoClient _coinGeckoClient;
    private readonly Dictionary<string, string> _tokenDict;
    protected override SourceType SourceType => SourceType.CoinGecko;

    public CoinGeckoTokenPriceSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TokenPriceSourceOptions> options, ILogger<TokenPriceSearchWorkerBase> baseLogger,
        IPriceProvider priceProvider, ICoinGeckoClient coinGeckoClient) : base(timer, serviceScopeFactory, options,
        baseLogger, priceProvider)
    {
        _coinGeckoClient = coinGeckoClient;
        var op = options.Value.GetSourceOption(SourceType);
        _coinIds = op.Tokens.Select(x => x.Split(',')[0]).ToArray();
        _tokenDict = op.Tokens.ToDictionary(x => x.Split(',')[0], x => x.Split(',')[1]);
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        BaseLogger.LogInformation("[CoinGecko] Search worker Start...");

        await CollectRealTimePricesAsync();
    }

    private async Task CollectRealTimePricesAsync()
    {
        try
        {
            await PriceProvider.UpdatePricesAsync(SourceType.CoinGecko,
                (await _coinGeckoClient.SimpleClient.GetSimplePrice(_coinIds, new[] { FiatSymbol })).Select(kv =>
                    new PriceDto
                    {
                        TokenPair = $"{_tokenDict[kv.Key]}-USD",
                        Price = PriceConvertHelper.ConvertPrice((double)kv.Value[FiatSymbol].Value),
                        UpdateTime = DateTime.Now
                    }).ToList());
        }
        catch (TaskCanceledException)
        {
            BaseLogger.LogWarning("[CoinGecko] Timeout of 100 seconds elapsing.");
        }
        catch (HttpRequestException e)
        {
            if (e.StatusCode == HttpStatusCode.TooManyRequests)
            {
                BaseLogger.LogWarning("[CoinGecko] Too Many Requests");
                Thread.Sleep(10000);
            }
        }
        catch (Exception e)
        {
            BaseLogger.LogError(e, "[CoinGecko] Query token price error.");
        }
    }

    private async Task CollectDailyPricesAsync() => await Task.WhenAll(_coinIds.Select(GetDailyPriceByIdAsync));

    private async Task GetDailyPriceByIdAsync(string coinId)
    {
        var tokenPair = $"{_tokenDict[coinId]}-USD";
        try
        {
            for (var i = 0; i < 30; i++)
            {
                var tempDay = DateTime.Today.AddDays(-i);
                var targetDay = new DateTime(tempDay.Year, tempDay.Month, tempDay.Day, 0, 0, 0);
                var result = await PriceProvider.GetDailyPriceAsync(targetDay, tokenPair);

                if (result != null) continue;

                BaseLogger.LogInformation($"Ready to get {tempDay:dd-MM-yyyy} {tokenPair} price");

                var fullData = await _coinGeckoClient.CoinsClient.GetHistoryByCoinId(coinId,
                    tempDay.ToString("dd-MM-yyyy"), "false");

                if (fullData.MarketData == null)
                {
                    BaseLogger.LogWarning($"Get {tempDay:dd-MM-yyyy} {tokenPair} price failed");
                    continue;
                }

                await PriceProvider.UpdateHourlyPriceAsync(new()
                {
                    TokenPair = tokenPair,
                    Price = PriceConvertHelper.ConvertPrice((double)fullData.MarketData.CurrentPrice[FiatSymbol].Value),
                    UpdateTime = targetDay
                });
            }
        }
        catch (HttpRequestException e)
        {
            if (e.StatusCode == HttpStatusCode.TooManyRequests)
            {
                BaseLogger.LogWarning("[CoinGecko] Too Many Requests");
                Thread.Sleep(10000);
            }
        }
        catch (Exception e)
        {
            BaseLogger.LogError(e, $"[CoinGecko] Query {tokenPair} token price error.");
        }
    }
}