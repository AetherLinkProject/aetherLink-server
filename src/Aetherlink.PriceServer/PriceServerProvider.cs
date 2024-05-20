using Aetherlink.PriceServer.Common;
using Aetherlink.PriceServer.Dtos;
using Aetherlink.PriceServer.Options;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace Aetherlink.PriceServer;

public interface IPriceServerProvider
{
    public Task<PriceResponseDto> GetTokenPriceAsync(GetTokenPriceRequestDto input);
    public Task<AggregatedPriceResponseDto> GetAggregatedTokenPriceAsync(GetAggregatedTokenPriceRequestDto input);
    public Task<PriceListResponseDto> GetTokenPriceListAsync(GetTokenPriceListRequestDto input);
    public Task<PriceForLast24HoursResponseDto> GetPriceForLast24HoursAsync(GetPriceForLast24HoursRequestDto input);
}

public class PriceServerProvider : IPriceServerProvider, ITransientDependency
{
    private const string V1 = "/api/v1/";
    private const string TOKEN_PRICE = V1 + "price";
    private const string TOKEN_PRICE_LIST = V1 + "prices";
    private const string LAST_24HOURS_PRICE = V1 + "prices/hours";
    private const string AGGREGATED_TOKEN_PRICE = V1 + "aggregatedPrice";

    private readonly IHttpService _http;
    private readonly TokenPriceOption _option;

    public PriceServerProvider(IHttpService httpClient, IOptionsSnapshot<TokenPriceOption> option)
    {
        _http = httpClient;
        _option = option.Value;
    }

    public async Task<PriceResponseDto> GetTokenPriceAsync(GetTokenPriceRequestDto input) =>
        await QueryAsync<PriceResponseDto>(TOKEN_PRICE, input, ContextHelper.GeneratorCtx());

    public async Task<AggregatedPriceResponseDto> GetAggregatedTokenPriceAsync(GetAggregatedTokenPriceRequestDto input)
        => await QueryAsync<AggregatedPriceResponseDto>(AGGREGATED_TOKEN_PRICE, input, ContextHelper.GeneratorCtx());

    public async Task<PriceListResponseDto> GetTokenPriceListAsync(GetTokenPriceListRequestDto input)
        => await QueryAsync<PriceListResponseDto>(TOKEN_PRICE_LIST, input, ContextHelper.GeneratorCtx());

    public async Task<PriceForLast24HoursResponseDto> GetPriceForLast24HoursAsync(
        GetPriceForLast24HoursRequestDto input) =>
        await QueryAsync<PriceForLast24HoursResponseDto>(LAST_24HOURS_PRICE, input, ContextHelper.GeneratorCtx());

    private async Task<T> QueryAsync<T>(string uri, object? data, CancellationToken ctx)
        where T : class => data == null
        ? await _http.GetAsync<T>(_option.BaseUrl + uri, ctx)
        : await _http.GetAsync<T>(_option.BaseUrl + uri, data, ctx);
}