using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
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

    [ExceptionHandler(typeof(Exception), TargetType = typeof(HistoricPriceProvider),
        MethodName = nameof(HandleException))]
    public virtual async Task<PriceDto> GetHistoricPriceAsync(string tokenPair, DateTime time)
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

    [ExceptionHandler(typeof(Exception), TargetType = typeof(HistoricPriceProvider), MethodName = nameof(HandleGetHistoricTokenPriceException))]
    public virtual async Task<long> GetHistoricTokenPriceAsync(string tokenPair, DateTime time)
    {
            return await _coinbaseProvider.GetHistoricPriceAsync(tokenPair, time);
    }

    [ExceptionHandler(typeof(Exception), Message = "Get CoinGecko historic price fail, will try it in CoinBase", ReturnDefault = ReturnDefault.Default)]
    public virtual async Task<long> GetHistoricPriceHandleAsync(string tokenPair, DateTime time)
    {
        return await _coinGeckoProvider.GetHistoricPriceAsync(tokenPair, time);
    }

    #region Exception handing

    public async Task<FlowBehavior> HandleException(Exception ex, string tokenPair, DateTime time)
    {
        _logger.LogError(ex, $"Get {tokenPair} historic {time:yyyy-MM-dd} failed.");

        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = null
        };
    }

    public async Task<FlowBehavior> HandleGetHistoricTokenPriceException(Exception ex, string tokenPair, DateTime time)
    {
        _logger.LogError("Get CoinBase historic price fail");

        var result = await GetHistoricPriceHandleAsync(tokenPair, time);

        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = result
        };
    }

    #endregion
}