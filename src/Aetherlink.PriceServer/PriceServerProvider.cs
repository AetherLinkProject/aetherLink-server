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
    public Task<LatestPriceListResponseDto> GetLatestTokenPriceListAsync(GetLatestTokenPriceListRequestDto input);
    public Task<PriceForLast24HoursResponseDto> GetPriceForLast24HoursAsync(GetPriceForLast24HoursRequestDto input);
    public Task<DailyPriceResponseDto> GetDailyPriceAsync(GetDailyPriceRequestDto input);
}

public class PriceServerProvider : IPriceServerProvider, ITransientDependency
{
    private readonly IHttpService _http;
    private readonly TokenPriceOption _option;

    public PriceServerProvider(IHttpService httpClient, IOptionsSnapshot<TokenPriceOption> option)
    {
        _http = httpClient;
        _option = option.Value;
    }

    public async Task<PriceResponseDto> GetTokenPriceAsync(GetTokenPriceRequestDto input) =>
        await QueryAsync<PriceResponseDto>(RouterConstants.TOKEN_PRICE_URI, AddAuthParams(input),
            ContextHelper.GeneratorCtx());

    public async Task<AggregatedPriceResponseDto> GetAggregatedTokenPriceAsync(GetAggregatedTokenPriceRequestDto input)
        => await QueryAsync<AggregatedPriceResponseDto>(RouterConstants.AGGREGATED_TOKEN_PRICE_URI,
            AddAuthParams(input), ContextHelper.GeneratorCtx());

    public async Task<PriceListResponseDto> GetTokenPriceListAsync(GetTokenPriceListRequestDto input)
        => await QueryAsync<PriceListResponseDto>(RouterConstants.TOKEN_PRICE_LIST_URI, AddAuthParams(input),
            ContextHelper.GeneratorCtx());

    public async Task<LatestPriceListResponseDto> GetLatestTokenPriceListAsync(GetLatestTokenPriceListRequestDto input)
        => await QueryAsync<LatestPriceListResponseDto>(RouterConstants.LATEST_TOKEN_PRICE_LIST_URI,
            AddAuthParams(input), ContextHelper.GeneratorCtx());

    public async Task<PriceForLast24HoursResponseDto> GetPriceForLast24HoursAsync(
        GetPriceForLast24HoursRequestDto input) =>
        await QueryAsync<PriceForLast24HoursResponseDto>(RouterConstants.LAST_24HOURS_PRICE_URI, AddAuthParams(input),
            ContextHelper.GeneratorCtx());

    public async Task<DailyPriceResponseDto> GetDailyPriceAsync(GetDailyPriceRequestDto input) =>
        await QueryAsync<DailyPriceResponseDto>(RouterConstants.DAILY_PRICE_URI, AddAuthParams(input),
            ContextHelper.GeneratorCtx());

    private async Task<T> QueryAsync<T>(string uri, object data, CancellationToken ctx)
        where T : class => await _http.GetAsync<T>(_option.BaseUrl + uri, data, ctx);

    private T AddAuthParams<T>(T input) where T : AuthDto
    {
        input.AppId = _option.ApiKey;
        return input;
    }
}