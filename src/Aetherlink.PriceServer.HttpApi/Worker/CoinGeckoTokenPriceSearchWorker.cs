using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Common;
using AetherlinkPriceServer.Dtos;
using CoinGecko.Interfaces;
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
    private const string FiatSymbol = "usd";
    private readonly TokenPriceSourceOption _option;
    private readonly ICoinGeckoClient _coinGeckoClient;

    protected override SourceType SourceType => SourceType.CoinGecko;

    public CoinGeckoTokenPriceSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TokenPriceSourceOptions> options, ILogger<TokenPriceSearchWorkerBase> baseLogger,
        IPriceProvider priceProvider, IHttpClientService httpClient, ICoinGeckoClient coinGeckoClient) : base(timer,
        serviceScopeFactory, options,
        baseLogger, priceProvider)
    {
        _coinGeckoClient = coinGeckoClient;
        _option = options.Value.GetSourceOption(SourceType);
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        BaseLogger.LogInformation("coingecko token price...");
        // var tasks = _option.Tokens.Select(SyncPriceAsync);

        // var ids = _option.Tokens.Where(t => _coinIdMapping.TryGetValue(t.Split('-')[0].ToUpper(), out var coinId))
        //     .Select(_ => coinId).ToArray();
        var ids = _option.Tokens.Select(t =>
        {
            _coinIdMapping.TryGetValue(t.Split('-')[0].ToUpper(), out var coinId);
            return coinId;
        }).Where(id => id != null).ToArray();
       var coinData =  await _coinGeckoClient.SimpleClient.GetSimplePrice(ids, new[] { FiatSymbol });
        // string[] tokens = input.Split('-');
        // string quoteTokenName = tokens[0];
        // string baseTokenName = tokens[1];
        // await Task.WhenAll(tasks);
        BaseLogger.LogInformation($"[CoinGecko]Get  price ...");

    }

    private async Task SyncPriceAsync(string tokenPair)
    {
        BaseLogger.LogInformation($"[CoinGecko]Get {tokenPair} price ...");

        if (!_coinIdMapping.TryGetValue("ELF", out var coinId)) return;
        try
        {
            var coinData =
                await _coinGeckoClient.SimpleClient.GetSimplePrice(new[] { "aelf", "ethereum", "solana" },
                    new[] { FiatSymbol });
            await PriceProvider.UpdatePrice(GenerateId(tokenPair), new PriceDto
            {
                TokenPair = tokenPair,
                Price = !coinData.TryGetValue(coinId, out var value)
                    ? 0
                    : Convert.ToInt64(value[FiatSymbol].Value),
                Decimal = 8,
                UpdateTime = DateTime.Now
            });
        }
        catch (Exception e)
        {
            BaseLogger.LogError(e, "[Coingecko] Can not get current price.");
            throw;
        }
    }

    private string GenerateId(string token) => IdGeneratorHelper.GenerateId(SourceType.CoinGecko, token.ToLower());

    private readonly Dictionary<string, string> _coinIdMapping = new()
    {
        { "ELF", "aelf" },
        { "USDT", "tether" },
        { "SETH", "ethereum" },
        { "GETH", "ethereum" },
        { "ETH", "ethereum" },
        { "OP", "optimism" },
        { "BSC", "binancecoin" },
        { "TRX", "tron" },
        { "SOL", "solana" },
        { "MATIC", "matic-network" },
        { "ARB", "arbitrum" }
    };
}