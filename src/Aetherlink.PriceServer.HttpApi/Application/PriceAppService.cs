using System;
using System.Linq;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Provider;
using Volo.Abp.DependencyInjection;

namespace AetherlinkPriceServer.Application;

public interface IPriceAppService
{
    public Task<PriceResponseDto> GetTokenPriceAsync(GetTokenPriceRequestDto input);
    public Task<AggregatedPriceResponseDto> GetAggregatedTokenPriceAsync(GetAggregatedTokenPriceRequestDto input);
    public Task<PriceListResponseDto> GetTokenPriceListAsync(GetTokenPriceListRequestDto input);
}

public class PriceAppService : IPriceAppService, ISingletonDependency
{
    private readonly IPriceProvider _priceProvider;

    public PriceAppService(IPriceProvider priceProvider)
    {
        _priceProvider = priceProvider;
    }

    public async Task<PriceResponseDto> GetTokenPriceAsync(GetTokenPriceRequestDto input) => new()
    {
        Source = input.Source.ToString(),
        Data = await _priceProvider.GetPriceAsync(input.TokenPair, input.Source)
    };

    public async Task<PriceListResponseDto> GetTokenPriceListAsync(GetTokenPriceListRequestDto input) => new()
    {
        Source = input.Source.ToString(),
        Prices = await _priceProvider.GetPriceListAsync(input.Source, input.TokenPairs)
    };

    public async Task<AggregatedPriceResponseDto> GetAggregatedTokenPriceAsync(GetAggregatedTokenPriceRequestDto input)
    {
        var price = input.AggregateType == AggregateType.Latest
            ? await _priceProvider.GetPriceAsync(input.TokenPair)
            : await GetAggregatedPriceAsync(input);

        return price != null
            ? new() { AggregateType = input.AggregateType.ToString(), Data = price }
            : null;
    }

    private async Task<PriceDto> GetAggregatedPriceAsync(GetAggregatedTokenPriceRequestDto input)
    {
        var prices = await _priceProvider.GetAllSourcePricesAsync(input.TokenPair);
        if (prices.Count == 0) return null;

        var aggregatedPrice = new PriceDto { TokenPair = input.TokenPair, UpdateTime = DateTime.Now };

        switch (input.AggregateType)
        {
            case AggregateType.Avg:
                aggregatedPrice.Price = (long)prices.Average(p => p.Price);
                break;
            case AggregateType.Medium:
                aggregatedPrice.Price = prices.OrderBy(p => p.Price).ElementAt((prices.Count - 1) / 2).Price;
                break;
            default:
                return null;
        }

        return aggregatedPrice;
    }
}