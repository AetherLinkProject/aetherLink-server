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
    private readonly ICoinGeckoProvider _coinGeckoProvider;
    private readonly ILogger<HistoricPriceProvider> _logger;

    public HistoricPriceProvider(ICoinBaseProvider coinbaseProvider, IPriceProvider priceProvider,
        ILogger<HistoricPriceProvider> logger, ICoinGeckoProvider coinGeckoProvider)
    {
        _logger = logger;
        _coinGeckoProvider = coinGeckoProvider;
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
                $"Get {tokenPair} historic {time:yyyy-MM-dd} price not found, ready to find it by third party api.");

            var thirdPartyPrice = await GetHistoricTokenPriceAsync(tokenPair, time);
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

    private async Task<long> GetHistoricTokenPriceAsync(string tokenPair, DateTime time)
    {
        try
        {
            return await _coinbaseProvider.GetHistoricPriceAsync(tokenPair, time);
        }
        catch (Exception)
        {
            _logger.LogError("Get CoinBase historic price fail");
            try
            {
                return await _coinGeckoProvider.GetHistoricPriceAsync(tokenPair, time);
            }
            catch (Exception)
            {
                _logger.LogError("Get CoinGecko historic price fail, will try it in CoinBase");
                return 0;
            }
        }
    }
}