using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Options;
using AetherlinkPriceServer.Provider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using Serilog;

namespace AetherlinkPriceServer.Worker;

public class BinancePriceSearchWorker : TokenPriceSearchWorkerBase
{
    private readonly ILogger _logger;
    private readonly TokenPriceSourceOption _option;
    private readonly IBinanceProvider _binanceProvider;
    protected override SourceType SourceType => SourceType.Binance;

    public BinancePriceSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TokenPriceSourceOptions> options, IPriceProvider priceProvider,
        IBinanceProvider binanceProvider) : base(timer, serviceScopeFactory, options, priceProvider)
    {
        _binanceProvider = binanceProvider;
        _option = options.Value.GetSourceOption(SourceType);
        _logger = Log.ForContext<BinancePriceSearchWorker>();
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.Information("[Binance] Search worker Start...");

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
            _logger.Warning("[Binance] Timeout of 100 seconds elapsing.");
            return new();
        }
        catch (HttpRequestException he)
        {
            if (he.Message.Contains("No route to host")) _logger.Warning("[Binance] Network error please check.");
            return new();
        }
        catch (Exception e)
        {
            _logger.Error(e, $"[Binance] Can not get {tokenPair} current price.");
            return new();
        }
    }
}