using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Options;
using AetherlinkPriceServer.Provider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherlinkPriceServer.Worker;

public class CoinGeckoTokenPriceSearchWorker : TokenPriceSearchWorkerBase
{
    private readonly ILogger _logger;
    private readonly List<string> _coinIds;
    private readonly ICoinGeckoProvider _coinGeckoProvider;
    protected override SourceType SourceType => SourceType.CoinGecko;

    public CoinGeckoTokenPriceSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TokenPriceSourceOptions> options, IPriceProvider priceProvider,
        ICoinGeckoProvider coinGeckoProvider) : base(timer, serviceScopeFactory, options, priceProvider)
    {
        _coinGeckoProvider = coinGeckoProvider;
        _coinIds = options.Value.GetSourceOption(SourceType).Tokens;
        _logger = Log.ForContext<CoinGeckoTokenPriceSearchWorker>();
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.Information("[CoinGecko] Search worker Start...");

        await CollectRealTimePricesAsync();
    }

    private async Task CollectRealTimePricesAsync()
    {
        try
        {
            await PriceProvider.UpdatePricesAsync(SourceType.CoinGecko,
                await _coinGeckoProvider.GetTokenPricesAsync(_coinIds));
        }
        catch (TaskCanceledException)
        {
            _logger.Warning("[CoinGecko] Timeout of 100 seconds elapsing.");
        }
        catch (HttpRequestException e)
        {
            if (e.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.Warning("[CoinGecko] Too Many Requests");
                Thread.Sleep(10000);
            }
        }
        catch (Exception e)
        {
            _logger.Error(e, "[CoinGecko] Query token price error.");
        }
    }
}