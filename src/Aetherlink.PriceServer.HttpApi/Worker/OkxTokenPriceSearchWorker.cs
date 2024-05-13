using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Helper;
using AetherlinkPriceServer.Options;
using AetherlinkPriceServer.Provider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using Okex.Net;

namespace AetherlinkPriceServer.Worker;

public class OkxTokenPriceSearchWorker : TokenPriceSearchWorkerBase
{
    private readonly List<string> _tokenList;
    protected override SourceType SourceType => SourceType.Okx;

    public OkxTokenPriceSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TokenPriceSourceOptions> options, ILogger<TokenPriceSearchWorkerBase> baseLogger,
        IPriceProvider priceProvider) : base(timer, serviceScopeFactory, options, baseLogger, priceProvider)
    {
        _tokenList = options.Value.GetTokenList(SourceType);
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var tasks = _tokenList.Select(SyncPriceAsync);
        await Task.WhenAll(tasks);
    }

    private async Task SyncPriceAsync(string tokenPair)
    {
        BaseLogger.LogInformation($"[OKX]Get {tokenPair} price ...");

        // try
        // {
        //     var api = new OkexClient();
        //     // api.SetApiCredentials(_priceFeedsOptions.Okex.ApiKey,_priceFeedsOptions.Okex.SecretKey,_priceFeedsOptions.Okex.Passphrase);
        //     var symbolPair = "elf-usdt";
        //     var price = (await api.GetTradesAsync(symbolPair)).Data?.OrderByDescending(p => p.Time).ToList();
        //     // if (price == null || price.Count == 0)
        //     // {
        //     //     return 0;
        //     // }
        //
        //     BaseLogger.LogInformation("[PriceDataProvider][Okex] response: {res}", price.First().Price);
        //     Convert.ToInt64(price.First().Price * (decimal)Math.Pow(10, SymbolPriceConstants.DefaultDecimal));
        // }
        // catch (Exception e)
        // {
        //     // BaseLogger.LogError(e, "[PriceDataProvider][Okex]can not get {symbol} current price.",
        //     //     priceData.BaseCurrency);
        //     throw;
        // }

        await PriceProvider.UpdatePrice(GenerateId(tokenPair), new PriceDto
        {
            TokenPair = tokenPair,
            Price = new Random().Next(0, 1000000000),
            Decimal = 8,
            UpdateTime = DateTime.Now
        });
    }

    private string GenerateId(string token) => IdGeneratorHelper.GenerateId(SourceType.Okx, token.ToLower());
}