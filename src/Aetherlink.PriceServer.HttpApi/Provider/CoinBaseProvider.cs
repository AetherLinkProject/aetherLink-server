using System;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Common;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Common;
using AetherlinkPriceServer.Options;
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

    public CoinBaseProvider(IHttpService http, IOptionsSnapshot<TokenPriceSourceOptions> options)
    {
        _http = http;
        _option = options.Value.GetSourceOption(SourceType.CoinBase);
    }

    public async Task<long> GetTokenPriceAsync(string tokenPair) => PriceConvertHelper.ConvertPrice(double.Parse(
        (await _http.GetAsync<CoinBaseResponseDto>(_option.BaseUrl + $"{tokenPair}/buy",
            ContextHelper.GeneratorCtx())).Data["amount"]));

    public async Task<long> GetHistoricPriceAsync(string tokenPair, DateTime time) => PriceConvertHelper.ConvertPrice(
        double.Parse((await _http.GetAsync<CoinBaseResponseDto>(
                _option.BaseUrl + $"{tokenPair}/spot?date={time:yyyy-MM-dd}", ContextHelper.GeneratorCtx()))
            .Data["amount"]));
}