using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Common;
using AetherlinkPriceServer.Reporter;
using AetherlinkPriceServer.Worker.Dtos;
using Binance.Spot;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;

namespace AetherlinkPriceServer.Provider;

public interface IBinanceProvider
{
    public Task<long> GetTokenPriceAsync(string tokenPair);
}

public class BinanceProvider : IBinanceProvider, ITransientDependency
{
    private readonly IPriceCollectReporter _reporter;

    public BinanceProvider(IPriceCollectReporter reporter)
    {
        _reporter = reporter;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(BinanceProvider), MethodName = nameof(HandleException),
        FinallyMethodName = nameof(FinallyHandler))]
    public virtual async Task<long> GetTokenPriceAsync(string tokenPair)
    {
        var price = PriceConvertHelper.ConvertPrice(double.Parse(JsonConvert.DeserializeObject<BinancePriceDto>(
            await new Market().SymbolPriceTicker(tokenPair.Replace("-", "").ToUpper())).Price));

        _reporter.RecordPriceCollected(SourceType.Binance, tokenPair, price);

        return price;
    }

    #region Exception Handing

    public async Task<FlowBehavior> HandleException(Exception ex)
    {
        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Rethrow,
        };
    }

    public async Task FinallyHandler(string tokenPair)
    {
        var timer = _reporter.GetPriceCollectLatencyTimer(SourceType.Binance, tokenPair);
        timer.ObserveDuration();
    }

    #endregion
}