using System.Threading;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Common;
using Aetherlink.PriceServer.Dtos;
using Aetherlink.PriceServer.Options;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace Aetherlink.PriceServer;

public interface IPriceServerProvider
{
    public Task<string> TestConnectivity();
    public Task<PriceResponseDto> GetTokenPriceAsync(GetTokenPriceRequestDto input);
    public Task<AggregatedPriceResponseDto> GetAggregatedTokenPriceAsync(GetAggregatedTokenPriceRequestDto input);
    public Task<PriceListResponseDto> GetTokenPriceListAsync(GetTokenPriceListRequestDto input);
}

public class PriceServerProvider : IPriceServerProvider, ITransientDependency
{
    private const string TEST_CONNECTIVITY = "/api/v1/ping";
    private const string TOKEN_PRICE = "/api/v1/price";
    private const string TOKEN_PRICE_LIST = "/api/v1/prices";
    private const string AGGREGATED_TOKEN_PRICE = "/api/v1/aggregatedPrice";

    private readonly IHttpService _http;
    private readonly TokenPriceOption _option;

    public PriceServerProvider(IHttpService httpClient, IOptionsSnapshot<TokenPriceOption> option)
    {
        _option = option.Value;
        _http = httpClient;
    }

    public async Task<string> TestConnectivity() =>
        await QueryAsync<string>(TEST_CONNECTIVITY, null, ContextHelper.GeneratorCtx());

    public async Task<PriceResponseDto> GetTokenPriceAsync(GetTokenPriceRequestDto input) =>
        await QueryAsync<PriceResponseDto>(TOKEN_PRICE, input, ContextHelper.GeneratorCtx());

    public async Task<AggregatedPriceResponseDto> GetAggregatedTokenPriceAsync(GetAggregatedTokenPriceRequestDto input)
        => await QueryAsync<AggregatedPriceResponseDto>(AGGREGATED_TOKEN_PRICE, input, ContextHelper.GeneratorCtx());

    public async Task<PriceListResponseDto> GetTokenPriceListAsync(GetTokenPriceListRequestDto input)
        => await QueryAsync<PriceListResponseDto>(TOKEN_PRICE_LIST, input, ContextHelper.GeneratorCtx());

    private async Task<T> QueryAsync<T>(string uri, object data, CancellationToken ctx)
        where T : class => data == null
        ? await _http.GetAsync<T>(_option.BaseUrl + uri, ctx)
        : await _http.GetAsync<T>(_option.BaseUrl + uri, data, ctx);
}