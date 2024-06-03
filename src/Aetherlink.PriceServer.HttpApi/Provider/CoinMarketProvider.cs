using System;
using System.Collections.Generic;
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

public interface ICoinMarketProvider
{
    public Task<long> GetTokenPriceAsync(string tokenPair);
}

public class CoinMarketProvider : ICoinMarketProvider, ITransientDependency
{
    private readonly IHttpService _httpService;
    private readonly TokenPriceSourceOption _option;
    private readonly IPriceCollectReporter _reporter;

    public CoinMarketProvider(IHttpService httpService, IPriceCollectReporter reporter,
        IOptionsSnapshot<TokenPriceSourceOptions> options)
    {
        _reporter = reporter;
        _httpService = httpService;
        _option = options.Value.GetSourceOption(SourceType.CoinMarket);
    }

    public async Task<long> GetTokenPriceAsync(string tokenPair)
    {
        var tp = tokenPair.Split('-');

        var price = PriceConvertHelper.ConvertPrice((await _httpService.GetAsync<CoinMarketResponseDto>(
            new Uri($"{_option.BaseUrl}?symbol={tp[0]}").ToString(), new Dictionary<string, string>
            {
                { "X-CMC_PRO_API_KEY", _option.ApiKey },
                { "Accepts", "application/json" },
            }, ContextHelper.GeneratorCtx())).Data[tp[0]].Quote[tp[1]].Price);

        _reporter.RecordPriceCollected(SourceType.CoinMarket, tokenPair, price);

        return price;
    }
}