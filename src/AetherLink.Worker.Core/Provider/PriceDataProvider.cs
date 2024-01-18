using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Consts;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using Binance.Spot;
using CoinGecko.Interfaces;
using Io.Gate.GateApi.Api;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Okex.Net;
using Volo.Abp.DependencyInjection;
using Convert = System.Convert;

namespace AetherLink.Worker.Core.Provider;

public interface IPriceDataProvider
{
    Task<long> GetPriceAsync(PriceDataDto priceData);
}

public class PriceDataProvider : IPriceDataProvider, ITransientDependency
{
    private readonly IHttpClientService _httpClient;
    private readonly ICoinGeckoClient _coinGeckoClient;
    private readonly ILogger<PriceDataProvider> _logger;
    private readonly PriceFeedsOptions _priceFeedsOptions;

    public PriceDataProvider(ILogger<PriceDataProvider> logger, IOptionsSnapshot<PriceFeedsOptions> priceFeedsOptions,
        ICoinGeckoClient coinGeckoClient, IHttpClientService httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        _coinGeckoClient = coinGeckoClient;
        _priceFeedsOptions = priceFeedsOptions.Value;
    }

    public async Task<long> GetPriceAsync(PriceDataDto priceData)
    {
        if (string.IsNullOrEmpty(priceData.BaseCurrency) || string.IsNullOrEmpty(priceData.QuoteCurrency)) return 0;

        _logger.LogInformation("[PriceDataProvider] PriceData Source:{src}", _priceFeedsOptions.Source);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_priceFeedsOptions.Timeout));

        try
        {
            return _priceFeedsOptions.Source switch
            {
                "CoinGecko" => await GetCoingeckoPriceAsync(priceData, cts.Token),
                "Coinbase" => await GetCoinBasePriceAsync(priceData, cts.Token),
                "CoinMarket" => await GetCoinMarketPriceAsync(priceData, cts.Token),
                "Gate.io" => await GetGateIoPriceAsync(priceData, cts.Token),
                "Binance" => await GetBinancePriceAsync(priceData, cts.Token),
                "Okex" => await GetOkxPriceAsync(priceData, cts.Token),
                _ => 0
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("[PriceDataProvider] {source} Query timed out.", _priceFeedsOptions.Source);
            throw;
        }
    }

    private async Task<long> GetBinancePriceAsync(PriceDataDto priceData, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("[PriceDataProvider][Binance] Start.");
            var market = new Market();
            var symbolPriceTicker =
                await market.SymbolPriceTicker($"{priceData.BaseCurrency}{priceData.QuoteCurrency}");
            var binancePriceDto = JsonConvert.DeserializeObject<BinancePriceDto>(symbolPriceTicker);
            return Convert.ToInt64(Math.Round(double.Parse(binancePriceDto.Price) *
                                              Math.Pow(10, SymbolPriceConst.DefaultDecimal)));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[PriceDataProvider][Binance] Parse response error.");
            throw;
        }
    }

    private async Task<long> GetCoinMarketPriceAsync(PriceDataDto priceData, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("[PriceDataProvider][CoinMarket] Start.");
            var head = new Dictionary<string, string>
            {
                { "X-CMC_PRO_API_KEY", _priceFeedsOptions.CoinMarket.ApiKey },
                { "Accepts", "application/json" },
            };
            var url = new UriBuilder(_priceFeedsOptions.CoinMarket.BaseUrl);
            var queryString = HttpUtility.ParseQueryString(string.Empty);
            queryString["symbol"] = priceData.BaseCurrency;
            url.Query = queryString.ToString();

            _logger.LogDebug("[PriceDataProvider][CoinMarket] Url:{url}", url.ToString());
            var response = await _httpClient.GetAsync<CoinMarketResponseDto>(url.ToString(), head, cancellationToken);
            return Convert.ToInt64(Math.Round(
                response.Data[priceData.BaseCurrency].Quote[priceData.QuoteCurrency].Price *
                Math.Pow(10, SymbolPriceConst.DefaultDecimal)));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[PriceDataProvider][CoinMarket] Parse response error.");
            throw;
        }
    }

    private async Task<long> GetGateIoPriceAsync(PriceDataDto priceData, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("[PriceDataProvider][GateIo] Start.");
            var spotApi = new SpotApi();
            var currencyPair = await spotApi.ListTickersAsync($"{priceData.BaseCurrency}_{priceData.QuoteCurrency}");
            if (currencyPair.IsNullOrEmpty())
            {
                return 0;
            }

            var last = currencyPair[0].Last;
            return Convert.ToInt64(Math.Round(double.Parse(last) * Math.Pow(10, SymbolPriceConst.DefaultDecimal)));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[PriceDataProvider][GateIo] Parse response error.");
            throw;
        }
    }

    private async Task<long> GetCoinBasePriceAsync(PriceDataDto priceData, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("[PriceDataProvider][Coinbase] Start.");
            var response = await _httpClient.GetAsync<CoinBaseResponseDto>(
                _priceFeedsOptions.CoinBase.BaseUrl + $"{priceData.BaseCurrency}-{priceData.QuoteCurrency}/buy",
                cancellationToken);

            return Convert.ToInt64(Math.Round(double.Parse(response.Data["amount"]) *
                                              Math.Pow(10, SymbolPriceConst.DefaultDecimal)));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[PriceDataProvider][Coinbase] Parse response error.");
            throw;
        }
    }

    private async Task<long> GetCoingeckoPriceAsync(PriceDataDto priceData, CancellationToken cancellationToken)
    {
        try
        {
            var coinId = _priceFeedsOptions.CoinGecko.CoinIdMapping.TryGetValue(priceData.BaseCurrency, out var id)
                ? id
                : null;
            var coinData =
                await _coinGeckoClient.SimpleClient.GetSimplePrice(new[] { coinId }, new[] { priceData.QuoteCurrency });
            _logger.LogInformation("[PriceDataProvider][Coingecko] response: {res}", coinData.ToString());
            return !coinData.TryGetValue(coinId, out var value)
                ? 0
                : Convert.ToInt64(value[priceData.QuoteCurrency.ToLower()].Value);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[PriceDataProvider][Coingecko] Can not get {symbol} current price.",
                priceData.BaseCurrency);
            throw;
        }
    }

    private async Task<long> GetOkxPriceAsync(PriceDataDto priceData, CancellationToken cancellationToken)
    {
        try
        {
            var api = new OkexClient();
            // api.SetApiCredentials(_priceFeedsOptions.Okex.ApiKey,_priceFeedsOptions.Okex.SecretKey,_priceFeedsOptions.Okex.Passphrase);
            var symbolPair = $"{priceData.BaseCurrency}-{priceData.QuoteCurrency}";
            var price = (await api.GetTradesAsync(symbolPair)).Data?.OrderByDescending(p => p.Time).ToList();
            if (price == null || price.Count == 0)
            {
                return 0;
            }

            _logger.LogInformation("[PriceDataProvider][Okex] response: {res}", price.First().Price);
            return Convert.ToInt64(price.First().Price * (decimal)Math.Pow(10, SymbolPriceConst.DefaultDecimal));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[PriceDataProvider][Okex]can not get {symbol} current price.", priceData.BaseCurrency);
            throw;
        }
    }
}