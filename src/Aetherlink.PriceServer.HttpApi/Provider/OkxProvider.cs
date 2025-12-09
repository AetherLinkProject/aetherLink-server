using System.Linq;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Common;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Common;
using AetherlinkPriceServer.Reporter;
using Okex.Net;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace AetherlinkPriceServer.Provider;

public interface IOkxProvider
{
    public Task<long> GetTokenPriceAsync(string tokenPair);
}

public class OkxProvider : IOkxProvider, ITransientDependency
{
    private readonly IPriceCollectReporter _reporter;

    public OkxProvider(IPriceCollectReporter reporter)
    {
        _reporter = reporter;
    }

    public async Task<long> GetTokenPriceAsync(string tokenPair)
    {
        var timer = _reporter.GetPriceCollectLatencyTimer(SourceType.Okx, tokenPair);
        try
        {
            var okexTrade = (await new OkexClient().GetTradesAsync(tokenPair, 1, ContextHelper.GeneratorCtx())).Data
                ?.FirstOrDefault();

            if (okexTrade == null) throw new UserFriendlyException($"Get Okex {tokenPair} price failed.");

            var price = PriceConvertHelper.ConvertPrice(okexTrade.Price);

            _reporter.RecordPriceCollected(SourceType.Okx, tokenPair, price);

            return price;
        }
        finally
        {
            timer.ObserveDuration();
        }
    }
}