using System;
using System.Linq;
using System.Net;
using System.Net.Http;
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

public class CoinMarketTokenPriceSearchWorker : TokenPriceSearchWorkerBase
{
    private readonly ILogger _logger;
    private readonly TokenPriceSourceOption _option;
    private readonly ICoinMarketProvider _coinMarketProvider;
    protected override SourceType SourceType => SourceType.CoinMarket;

    public CoinMarketTokenPriceSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TokenPriceSourceOptions> options, IPriceProvider priceProvider,
        ICoinMarketProvider coinMarketProvider) : base(timer, serviceScopeFactory, options, priceProvider)
    {
        _coinMarketProvider = coinMarketProvider;
        _option = options.Value.GetSourceOption(SourceType);
        _logger = Log.ForContext<CoinMarketTokenPriceSearchWorker>();
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.Information("[CoinMarket] Search worker Start...");

        await PriceProvider.UpdatePricesAsync(SourceType.CoinMarket,
            (await Task.WhenAll(_option.Tokens.Select(SearchTokenPriceAsync))).ToList());
    }

    private async Task<PriceDto> SearchTokenPriceAsync(string tokenPair)
    {
        try
        {
            return new()
            {
                TokenPair = tokenPair,
                Price = await _coinMarketProvider.GetTokenPriceAsync(tokenPair),
                UpdateTime = DateTime.Now
            };
        }

        catch (Exception ex)
        {
            switch (ex)
            {
                case HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests }:
                    _logger.Warning("[CoinMarket] Too Many Requests.");
                    break;
                case HttpRequestException:
                    if (ex.Message.Contains("Resource temporarily unavailable"))
                        _logger.Warning("[CoinMarket] Resource temporarily unavailable.");
                    break;
                case TaskCanceledException:
                    _logger.Warning("[CoinMarket] Operation timeout, need check the network.");
                    break;
                default:
                    _logger.Warning(ex, $"[CoinMarket] Can not get {tokenPair} current price.");
                    break;
            }

            return new();
        }
    }
}