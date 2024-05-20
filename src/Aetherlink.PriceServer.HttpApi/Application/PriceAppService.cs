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
    public Task<PriceForLast24HoursResponseDto> GetPriceForLast24HoursAsync(GetPriceForLast24HoursRequestDto input);
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
            : new();
    }

    public async Task<PriceForLast24HoursResponseDto> GetPriceForLast24HoursAsync(
        GetPriceForLast24HoursRequestDto input)
    {
        var prices = (await _priceProvider.GetHourlyPriceAsync(input.TokenPair)).OrderBy(t => t.UpdateTime).ToList();

        var changeRage = prices.Count > 2
            ? Math.Round((double)(prices.Last().Price - prices.First().Price) / prices.First().Price, 5)
            : 0;

        return new()
            { Prices = prices.ToList(), ChangeRate24Hours = changeRage };
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