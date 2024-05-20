using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Common;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Common;
using AetherlinkPriceServer.Options;
using AetherlinkPriceServer.Provider;
using AetherlinkPriceServer.Worker.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherlinkPriceServer.Worker;

public class CoinMarketTokenPriceSearchWorker : TokenPriceSearchWorkerBase
{
    private const string FiatSymbol = "USD";
    private readonly IHttpService _httpService;
    private readonly TokenPriceSourceOption _option;
    protected override SourceType SourceType => SourceType.CoinMarket;

    public CoinMarketTokenPriceSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TokenPriceSourceOptions> options, ILogger<TokenPriceSearchWorkerBase> baseLogger,
        IPriceProvider priceProvider, IHttpService httpService) : base(timer, serviceScopeFactory, options,
        baseLogger, priceProvider)
    {
        _httpService = httpService;
        _option = options.Value.GetSourceOption(SourceType);
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        BaseLogger.LogInformation("[CoinMarket] Search worker Start...");

        await PriceProvider.UpdatePricesAsync(SourceType.CoinMarket,
            (await Task.WhenAll(_option.Tokens.Select(SearchTokenPriceAsync))).ToList());
    }

    private async Task<PriceDto> SearchTokenPriceAsync(string tokenPair)
    {
        try
        {
            var tp = tokenPair.Split('-');

            return new()
            {
                TokenPair = tokenPair,
                Price = PriceConvertHelper.ConvertPrice((await _httpService.GetAsync<CoinMarketResponseDto>(
                    new Uri($"{_option.BaseUrl}?symbol={tp[0]}").ToString(), new Dictionary<string, string>
                    {
                        { "X-CMC_PRO_API_KEY", _option.ApiKey },
                        { "Accepts", "application/json" },
                    }, ContextHelper.GeneratorCtx())).Data[tp[0]].Quote[FiatSymbol].Price),
                UpdateTime = DateTime.Now
            };
        }

        catch (Exception ex)
        {
            switch (ex)
            {
                case HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests }:
                    BaseLogger.LogError("[CoinMarket] Too Many Requests.");
                    break;
                case TaskCanceledException:
                    BaseLogger.LogWarning("[CoinMarket] Operation canceled, need check the network.");
                    break;
                default:
                    BaseLogger.LogError(ex, $"[CoinMarket] Can not get {tokenPair} current price.");
                    break;
            }

            return new();
        }
    }
}