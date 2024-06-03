using System;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Common;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Common;
using AetherlinkPriceServer.Options;
using AetherlinkPriceServer.Reporter;
using AetherlinkPriceServer.Worker.Dtos;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AetherlinkPriceServer.Provider;

public interface ICoinBaseProvider
{
    public Task<long> GetTokenPriceAsync(string tokenPair);
    public Task<long> GetHistoricPriceAsync(string tokenPair, DateTime time);
}

public class CoinBaseProvider : ICoinBaseProvider, ITransientDependency
{
    private readonly IHttpService _http;
    private readonly TokenPriceSourceOption _option;
    private readonly IPriceCollectReporter _reporter;

    public CoinBaseProvider(IHttpService http, IOptionsSnapshot<TokenPriceSourceOptions> options,
        IPriceCollectReporter reporter)
    {
        _http = http;
        _reporter = reporter;
        _option = options.Value.GetSourceOption(SourceType.CoinBase);
    }

    public async Task<long> GetTokenPriceAsync(string tokenPair)
    {
        var price = PriceConvertHelper.ConvertPrice(double.Parse(
            (await _http.GetAsync<CoinBaseResponseDto>(_option.BaseUrl + $"{tokenPair}/buy",
                ContextHelper.GeneratorCtx())).Data["amount"]));

        _reporter.RecordPriceCollected(SourceType.CoinBase, tokenPair, price);

        return price;
    }

    public async Task<long> GetHistoricPriceAsync(string tokenPair, DateTime time)
        => PriceConvertHelper.ConvertPrice(double.Parse(
            (await _http.GetAsync<CoinBaseResponseDto>(_option.BaseUrl + $"{tokenPair}/spot?date={time:yyyy-MM-dd}",
                ContextHelper.GeneratorCtx())).Data["amount"]));
}