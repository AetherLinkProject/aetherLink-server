using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Common;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Common;
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
    private readonly TokenPriceSourceOption _option;
    protected override SourceType SourceType => SourceType.Okx;

    public OkxTokenPriceSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TokenPriceSourceOptions> options, ILogger<TokenPriceSearchWorkerBase> baseLogger,
        IPriceProvider priceProvider) : base(timer, serviceScopeFactory, options, baseLogger, priceProvider)
    {
        _option = options.Value.GetSourceOption(SourceType);
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        BaseLogger.LogInformation("[OKX] Search worker Start...");

        await PriceProvider.UpdatePricesAsync(SourceType.Okx,
            (await Task.WhenAll(_option.Tokens.Select(SearchTokenPriceAsync))).ToList());
    }

    private async Task<PriceDto> SearchTokenPriceAsync(string tokenPair)
    {
        try
        {
            var price = (await new OkexClient().GetTradesAsync(tokenPair, 1, ContextHelper.GeneratorCtx())).Data
                ?.FirstOrDefault();
            if (price != null)
                return new()
                {
                    TokenPair = tokenPair,
                    Price = PriceConvertHelper.ConvertPrice(price.Price),
                    UpdateTime = DateTime.Now
                };

            BaseLogger.LogWarning($"[OKX] Token {tokenPair} price returned is empty.");
            return new();
        }
        catch (Exception e)
        {
            BaseLogger.LogError(e, $"[OKX] Can not get {tokenPair} current price.");
            return new();
        }
    }
}