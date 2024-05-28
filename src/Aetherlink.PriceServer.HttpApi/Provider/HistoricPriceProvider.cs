using System;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Dtos;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AetherlinkPriceServer.Provider;

public interface IHistoricPriceProvider
{
    public Task<PriceDto> GetHistoricPriceAsync(string tokenPair, DateTime time);
}

public class HistoricPriceProvider : IHistoricPriceProvider, ITransientDependency
{
    private readonly IPriceProvider _priceProvider;
    private readonly ICoinBaseProvider _coinbaseProvider;
    private readonly ILogger<HistoricPriceProvider> _logger;

    public HistoricPriceProvider(ICoinBaseProvider coinbaseProvider, IPriceProvider priceProvider,
        ILogger<HistoricPriceProvider> logger)
    {
        _logger = logger;
        _priceProvider = priceProvider;
        _coinbaseProvider = coinbaseProvider;
    }

    public async Task<PriceDto> GetHistoricPriceAsync(string tokenPair, DateTime time)
    {
        try
        {
            // get historical price
            var result = await _priceProvider.GetDailyPriceAsync(time, tokenPair);
            if (result != null) return result;

            _logger.LogInformation(
                $"Get {tokenPair} historic {time:yyyy-MM-dd} price not found, ready find in third party.");

            // todo: add coingecko price
            var thirdPartyPrice = await _coinbaseProvider.GetHistoricPriceAsync(tokenPair, time);

            if (thirdPartyPrice == 0) return null;

            // if not existing, query new price and save price
            var newHistoricPrice = new PriceDto
            {
                TokenPair = tokenPair,
                Price = thirdPartyPrice,
                UpdateTime = time
            };

            await _priceProvider.UpdateHourlyPriceAsync(newHistoricPrice);

            return newHistoricPrice;
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Get {tokenPair} historic {time:yyyy-MM-dd} failed.");
            return null;
        }
    }
}