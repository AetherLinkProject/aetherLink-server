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

        try
        {
            await PriceProvider.UpdatePricesAsync(SourceType.CoinGecko,
                (await _coinGeckoClient.SimpleClient.GetSimplePrice(_coinIds, new[] { FiatSymbol })).Select(kv =>
                    new KeyValuePair<string, PriceDto>($"{_tokenDict[kv.Key]}-USDT", new PriceDto
                    {
                        TokenPair = $"{_tokenDict[kv.Key]}-USDT",
                        Price = PriceConvertHelper.ConvertPrice((double)kv.Value[FiatSymbol].Value),
                        UpdateTime = DateTime.Now
                    })).ToArray());
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
}