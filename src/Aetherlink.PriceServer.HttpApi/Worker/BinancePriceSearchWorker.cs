using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Common;
using AetherlinkPriceServer.Options;
using AetherlinkPriceServer.Provider;
using AetherlinkPriceServer.Worker.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Binance.Spot;
using Newtonsoft.Json;
using Volo.Abp.Threading;

namespace AetherlinkPriceServer.Worker;

public class BinancePriceSearchWorker : TokenPriceSearchWorkerBase
{
    private readonly TokenPriceSourceOption _option;
    protected override SourceType SourceType => SourceType.Binance;

    public BinancePriceSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TokenPriceSourceOptions> options, ILogger<TokenPriceSearchWorkerBase> baseLogger,
        IPriceProvider priceProvider) : base(timer, serviceScopeFactory, options, baseLogger, priceProvider)
    {
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
                Price = PriceConvertHelper.ConvertPrice(
                    double.Parse(JsonConvert
                        .DeserializeObject<BinancePriceDto>(
                            await new Market().SymbolPriceTicker(tokenPair.Replace("-", "").ToUpper())).Price)),
                UpdateTime = DateTime.Now
            };
        }
        catch (TaskCanceledException)
        {
            BaseLogger.LogWarning("[Binance] Timeout of 100 seconds elapsing.");
            return new();
        }
        catch (Exception e)
        {
            BaseLogger.LogError(e, $"[Binance] Can not get {tokenPair} current price.");
            return new();
        }
    }
}