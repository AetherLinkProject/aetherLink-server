using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Options;
using AetherlinkPriceServer.Provider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherlinkPriceServer.Worker;

public class BinancePriceSearchWorker : TokenPriceSearchWorkerBase
{
    private readonly TokenPriceSourceOption _option;
    private readonly IBinanceProvider _binanceProvider;
    protected override SourceType SourceType => SourceType.Binance;

    public BinancePriceSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TokenPriceSourceOptions> options, ILogger<TokenPriceSearchWorkerBase> baseLogger,
        IPriceProvider priceProvider, IBinanceProvider binanceProvider) : base(timer, serviceScopeFactory, options,
        baseLogger, priceProvider)
    {
        _binanceProvider = binanceProvider;
        _option = options.Value.GetSourceOption(SourceType);
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        BaseLogger.LogInformation("[Binance] Search worker Start...");

        await PriceProvider.UpdatePricesAsync(SourceType.Binance,
            (await Task.WhenAll(_option.Tokens.Select(SearchTokenPriceAsync))).ToList());
    }

    private async Task<PriceDto> SearchTokenPriceAsync(string tokenPair)
    {
        try
        {
            return new()
            {
                TokenPair = tokenPair,
                Price = await _binanceProvider.GetTokenPriceAsync(tokenPair),
                UpdateTime = DateTime.Now
            };
        }
        catch (TaskCanceledException)
        {
            BaseLogger.LogWarning("[Binance] Timeout of 100 seconds elapsing.");
            return new();
        }
        catch (HttpRequestException he)
        {
            if (he.Message.Contains("No route to host")) BaseLogger.LogWarning("[Binance] Network error please check.");
            return new();
        }
        catch (Exception e)
        {
            BaseLogger.LogError(e, $"[Binance] Can not get {tokenPair} current price.");
            return new();
        }
    }
}