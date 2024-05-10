using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Helper;
using AetherlinkPriceServer.Options;
using AetherlinkPriceServer.Provider;
using Microsoft.Extensions.Options;
using Volo.Abp;
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
    private readonly TokenPriceSourceOptions _sourceOptions;

    public PriceAppService(IPriceProvider priceProvider, IOptionsSnapshot<TokenPriceSourceOptions> sourceOptions)
    {
        _priceProvider = priceProvider;
        _sourceOptions = sourceOptions.Value;
    }

    public async Task<PriceResponseDto> GetTokenPriceAsync(GetTokenPriceRequestDto input)
    {
        FilterSupportedToken(input.Source, new List<string> { input.TokenPair });

        return new PriceResponseDto
        {
            Source = input.Source.ToString(),
            Data = await _priceProvider.GetPriceAsync(GenerateKey(input.Source, input.TokenPair))
        };
    }

    public async Task<PriceListResponseDto> GetTokenPriceListAsync(GetTokenPriceListRequestDto input)
    {
        var tokenList = FilterSupportedToken(input.Source, input.TokenPairs);

        return new PriceListResponseDto
        {
            Source = input.Source.ToString(),
            Prices = await _priceProvider.GetPriceListAsync(
                tokenList.Select(t => GenerateKey(input.Source, t)).ToList())
        };
    }

    public async Task<AggregatedPriceResponseDto> GetAggregatedTokenPriceAsync(GetAggregatedTokenPriceRequestDto input)
    {
        var price = new PriceDto { TokenPair = input.TokenPair };

        if (input.AggregateType == AggregateType.Latest)
        {
            FilterSupportedToken(SourceType.CoinGecko, new List<string> { input.TokenPair });
            price = await _priceProvider.GetPriceAsync(GenerateKey(SourceType.CoinGecko, input.TokenPair));
        }
        else
        {
            var prices = await GetPriceListAsync(input.TokenPair);
            price.Decimal = prices.First().Decimal;
            price.UpdateTime = DateTime.Now;

            switch (input.AggregateType)
            {
                case AggregateType.Avg:
                    price.Price = (long)prices.Average(p => p.Price);
                    break;
                case AggregateType.Medium:
                    price.Price = prices.OrderBy(p => p.Price).ElementAt((prices.Count - 1) / 2).Price;
                    break;
            }
        }

        return new AggregatedPriceResponseDto
        {
            AggregateType = input.AggregateType.ToString(),
            Data = price
        };
    }

    private async Task<List<PriceDto>> GetPriceListAsync(string tokenPair)
        => await _priceProvider.GetPriceListAsync(FilterSupportedSource(tokenPair)
            .Select(e => GenerateKey(e, tokenPair)).ToList());

    private List<string> FilterSupportedSource(string tokenPair)
    {
        var sourceList = _sourceOptions.Sources.Where(x => x.Value.Tokens.Contains(tokenPair.ToUpper()))
            .Select(x => x.Value.Name).ToList();
        if (sourceList.Count == 0) throw new UserFriendlyException($"Not supported tokenPair: {tokenPair}");
        return sourceList;
    }

    private List<string> FilterSupportedToken(SourceType source, IEnumerable<string> tokens)
    {
        if (!_sourceOptions.Sources.TryGetValue(source.ToString(), out var sourceOption))
            throw new UserFriendlyException($"Not supported source: {source}");

        var tokenList = sourceOption.Tokens.Intersect(tokens, StringComparer.OrdinalIgnoreCase).ToList();
        if (tokenList.Count == 0) throw new UserFriendlyException($"{source} not support those tokens");

        return tokenList;
    }

    private string GenerateKey(object source, string key) => IdGeneratorHelper.GenerateId(source, key);
}