using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Common;
using AetherlinkPriceServer.Dtos;
using AetherlinkPriceServer.Helper;
using AetherlinkPriceServer.Options;
using AetherlinkPriceServer.Provider;
using AetherlinkPriceServer.Worker.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherlinkPriceServer.Worker;

public class CoinGeckoTokenPriceSearchWorker : TokenPriceSearchWorkerBase
{
    private readonly IHttpClientService _httpClient;
    private readonly TokenPriceSourceOption _option;
    protected override SourceType SourceType => SourceType.CoinGecko;

    public CoinGeckoTokenPriceSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TokenPriceSourceOptions> options, ILogger<TokenPriceSearchWorkerBase> baseLogger,
        IPriceProvider priceProvider, IHttpClientService httpClient) : base(timer, serviceScopeFactory, options,
        baseLogger, priceProvider)
    {
        _httpClient = httpClient;
        _option = options.Value.GetSourceOption(SourceType);
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        BaseLogger.LogInformation("coingecko token price...");
        var tasks = _option.Tokens.Select(SyncPriceAsync);
        await Task.WhenAll(tasks);
    }

    private async Task SyncPriceAsync(string tokenPair)
    {
        BaseLogger.LogInformation($"[CoinGecko]Get {tokenPair} price ...");
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(NetworkConstants.DefaultTimout));

        try
        {
            BaseLogger.LogInformation("[PriceDataProvider][CoinMarket] Start.");
            var head = new Dictionary<string, string>
            {
                { "X-CMC_PRO_API_KEY", _option.ApiKey },
                { "Accepts", "application/json" },
            };
            var url = new UriBuilder(_option.BaseUrl);
            var queryString = HttpUtility.ParseQueryString(string.Empty);
            queryString["symbol"] = "elf";
            url.Query = queryString.ToString();

            BaseLogger.LogDebug("[PriceDataProvider][CoinMarket] Url:{url}", url.ToString());
            var response = await _httpClient.GetAsync<CoinMarketResponseDto>(url.ToString(), head, cts.Token);
            Convert.ToInt64(Math.Round(response.Data["elf"].Quote["usdt"].Price *
                                       Math.Pow(10, SymbolPriceConstants.DefaultDecimal)));
        }
        catch (Exception e)
        {
            BaseLogger.LogError(e, "[PriceDataProvider][CoinMarket] Parse response error.");
            throw;
        }


        // await PriceProvider.UpdatePrice(GenerateId(tokenPair), new PriceDto
        // {
        //     TokenPair = tokenPair,
        //     Price = new Random().Next(0, 1000000000),
        //     Decimal = 8,
        //     UpdateTime = DateTime.Now
        // });
    }

    private string GenerateId(string token) => IdGeneratorHelper.GenerateId(SourceType.CoinGecko, token.ToLower());
}