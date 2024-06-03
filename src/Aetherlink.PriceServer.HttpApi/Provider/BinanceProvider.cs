using System.Threading.Tasks;
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

    public async Task<long> GetTokenPriceAsync(string tokenPair)
    {
        var price = PriceConvertHelper.ConvertPrice(double.Parse(JsonConvert.DeserializeObject<BinancePriceDto>(
            await new Market().SymbolPriceTicker(tokenPair.Replace("-", "").ToUpper())).Price));

        _reporter.RecordPriceCollected(SourceType.Binance, tokenPair, price);
        
        return price;
    }
}