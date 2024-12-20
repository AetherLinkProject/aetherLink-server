using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Provider;
using AetherlinkPriceServer.Reporter;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AetherlinkPriceServer.Application;

public interface IPriceAppService
{
    public Task<PriceResponseDto> GetTokenPriceAsync(GetTokenPriceRequestDto input);
    public Task<AggregatedPriceResponseDto> GetAggregatedTokenPriceAsync(GetAggregatedTokenPriceRequestDto input);
    public Task<PriceListResponseDto> GetTokenPriceListAsync(GetTokenPriceListRequestDto input);
    public Task<LatestPriceListResponseDto> GetLatestTokenPriceListAsync(GetLatestTokenPriceListRequestDto input);
    public Task<PriceForLast24HoursResponseDto> GetPriceForLast24HoursAsync(GetPriceForLast24HoursRequestDto input);
    public Task<DailyPriceResponseDto> GetDailyPriceAsync(GetDailyPriceRequestDto input);
}

public class PriceAppService : IPriceAppService, ISingletonDependency
{
    private readonly IPriceProvider _priceProvider;
    private readonly IPriceQueryReporter _reporter;
    private readonly ILogger<PriceAppService> _logger;
    private readonly IHistoricPriceProvider _historicPriceProvider;

    public PriceAppService(IPriceProvider priceProvider, IHistoricPriceProvider historicPriceProvider,
        IPriceQueryReporter reporter, ILogger<PriceAppService> logger)
    {
        _logger = logger;
        _reporter = reporter;
        _priceProvider = priceProvider;
        _historicPriceProvider = historicPriceProvider;
    }

    public async Task<PriceResponseDto> GetTokenPriceAsync(GetTokenPriceRequestDto input)
    {
        _logger.LogDebug($"Get {input.AppId} GetTokenPriceAsync request. ");
        var timer = _reporter.GetPriceRequestLatencyTimer(input.AppId, RouterConstants.TOKEN_PRICE_URI);
        try
        {
            _reporter.RecordPriceQueriedTotal(input.AppId, RouterConstants.TOKEN_PRICE_URI);

            return new()
            {
                Source = input.Source.ToString(),
                Data = await _priceProvider.GetPriceAsync(input.TokenPair, input.Source)
            };
        }
        finally
        {
            timer.ObserveDuration();
        }
    }

    public async Task<PriceListResponseDto> GetTokenPriceListAsync(GetTokenPriceListRequestDto input)
    {
        _logger.LogDebug($"Get {input.AppId} GetTokenPriceListAsync request. ");
        var timer = _reporter.GetPriceRequestLatencyTimer(input.AppId, RouterConstants.TOKEN_PRICE_LIST_URI);
        try
        {
            _reporter.RecordPriceQueriedTotal(input.AppId, RouterConstants.TOKEN_PRICE_LIST_URI);

            return new()
            {
                Source = input.Source.ToString(),
                Prices = await _priceProvider.GetPriceListAsync(input.Source, input.TokenPairs)
            };
        }
        finally
        {
            timer.ObserveDuration();
        }
    }

    public async Task<LatestPriceListResponseDto> GetLatestTokenPriceListAsync(GetLatestTokenPriceListRequestDto input)
    {
        _logger.LogDebug($"Get {input.AppId} GetLatestTokenPriceListAsync request. ");

        var timer = _reporter.GetPriceRequestLatencyTimer(input.AppId, RouterConstants.LATEST_TOKEN_PRICE_LIST_URI);
        try
        {
            _reporter.RecordPriceQueriedTotal(input.AppId, RouterConstants.LATEST_TOKEN_PRICE_LIST_URI);

            return new()
            {
                Prices = await _priceProvider.GetPriceListAsync(input.TokenPairs)
            };
        }
        finally
        {
            timer.ObserveDuration();
        }
    }

    public async Task<AggregatedPriceResponseDto> GetAggregatedTokenPriceAsync(GetAggregatedTokenPriceRequestDto input)
    {
        _logger.LogDebug($"Get {input.AppId} GetAggregatedTokenPriceAsync request. ");
        var timer = _reporter.GetPriceRequestLatencyTimer(input.AppId, RouterConstants.AGGREGATED_TOKEN_PRICE_URI);
        try
        {
            _reporter.RecordPriceQueriedTotal(input.AppId, RouterConstants.AGGREGATED_TOKEN_PRICE_URI);
            _reporter.RecordAggregatedPriceQueriedTotal(input.AppId, input.TokenPair, input.AggregateType.ToString());

            var price = input.AggregateType == AggregateType.Latest
                ? await _priceProvider.GetPriceAsync(input.TokenPair)
                : await GetAggregatedPriceAsync(input);

            return price != null
                ? new() { AggregateType = input.AggregateType.ToString(), Data = price }
                : new();
        }
        finally
        {
            timer.ObserveDuration();
        }
    }

    public async Task<PriceForLast24HoursResponseDto> GetPriceForLast24HoursAsync(
        GetPriceForLast24HoursRequestDto input)
    {
        _logger.LogDebug($"Get {input.AppId} GetPriceForLast24HoursAsync request. ");
        var timer = _reporter.GetPriceRequestLatencyTimer(input.AppId, RouterConstants.LAST_24HOURS_PRICE_URI);
        try
        {
            _reporter.RecordPriceQueriedTotal(input.AppId, RouterConstants.LAST_24HOURS_PRICE_URI);

            var prices = (await _priceProvider.GetLatest24HoursPriceAsync(input.TokenPair)).OrderBy(t => t.UpdateTime)
                .ToList();

            var changeRage = prices.Count > 2
                ? Math.Round((double)(prices.Last().Price - prices.First().Price) / prices.First().Price, 5)
                : 0;

            return new()
                { Prices = prices.ToList(), ChangeRate24Hours = changeRage };
        }
        finally
        {
            timer.ObserveDuration();
        }
    }

    public async Task<DailyPriceResponseDto> GetDailyPriceAsync(GetDailyPriceRequestDto input)
    {
        _logger.LogDebug($"Get {input.AppId} GetDailyPriceAsync request. ");
        var timer = _reporter.GetPriceRequestLatencyTimer(input.AppId, RouterConstants.DAILY_PRICE_URI);
        try
        {
            _reporter.RecordPriceQueriedTotal(input.AppId, RouterConstants.DAILY_PRICE_URI);

            DateTime.TryParseExact(input.TimeStamp, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var targetTime);

            return new() { Data = await _historicPriceProvider.GetHistoricPriceAsync(input.TokenPair, targetTime) };
        }
        finally
        {
            timer.ObserveDuration();
        }
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