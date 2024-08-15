using System.Threading.Tasks;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Common;
using AetherlinkPriceServer.Reporter;
using Io.Gate.GateApi.Api;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace AetherlinkPriceServer.Provider;

public interface IGateIoProvider
{
    public Task<long> GetTokenPriceAsync(string tokenPair);
}

public class GateIoProvider : IGateIoProvider, ITransientDependency
{
    private readonly IPriceCollectReporter _reporter;

    public GateIoProvider(IPriceCollectReporter reporter)
    {
        _reporter = reporter;
    }

    public async Task<long> GetTokenPriceAsync(string tokenPair)
    {
        var currencyPair = await new SpotApi().ListTickersAsync(tokenPair.Replace("-", "_"));

        if (currencyPair == null || currencyPair.Count == 0)
            throw new UserFriendlyException("[GateIo] Get token {tokenPair} price error.");

        var price = PriceConvertHelper.ConvertPrice(double.Parse(currencyPair[0].Last));

        _reporter.RecordPriceCollected(SourceType.GateIo, tokenPair, price);

        return price;
    }
}