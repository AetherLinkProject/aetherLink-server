using System;
using System.Threading.Tasks;
using Aetherlink.PriceServer;
using Aetherlink.PriceServer.Dtos;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Reporter;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IPriceFeedsProvider
{
    public Task<long> GetPriceFeedsDataAsync(string currencyPair);
}

public class PriceFeedsProvider : IPriceFeedsProvider, ITransientDependency
{
    private readonly PriceFeedsOptions _options;
    private readonly IPriceFeedsReporter _reporter;
    private readonly ILogger<PriceFeedsProvider> _logger;
    private readonly IPriceServerProvider _priceServerProvider;

    public PriceFeedsProvider(IOptionsSnapshot<PriceFeedsOptions> priceFeedsOptions,
        IPriceServerProvider priceServerProvider, ILogger<PriceFeedsProvider> logger, IPriceFeedsReporter reporter)
    {
        _logger = logger;
        _reporter = reporter;
        _options = priceFeedsOptions.Value;
        _priceServerProvider = priceServerProvider;
    }

    public async Task<long> GetPriceFeedsDataAsync(string currencyPair)
    {
        var tokenPair = currencyPair.Replace("/", "-").ToLower();
        try
        {
            long price;
            if (_options.SourceType != SourceType.None)
            {
                var priceResult = await _priceServerProvider.GetTokenPriceAsync(new()
                {
                    TokenPair = tokenPair,
                    Source = _options.SourceType
                });
                price = priceResult.Data.Price;
            }
            else
            {
                var aggregatedPriceResult = await _priceServerProvider.GetAggregatedTokenPriceAsync(new()
                {
                    TokenPair = tokenPair,
                    AggregateType = _options.AggregateType
                });
                price = aggregatedPriceResult.Data.Price;
            }

            _logger.LogInformation($"[PriceFeeds] Get {tokenPair} price: {price}");
            
            _reporter.RecordPrice(currencyPair, price);
            
            return price;
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[PriceFeeds] Get {tokenPair} price failed.");
            return 0;
        }
    }
}