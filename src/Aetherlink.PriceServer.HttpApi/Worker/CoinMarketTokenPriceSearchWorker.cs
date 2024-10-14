using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Options;
using AetherlinkPriceServer.Provider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherlinkPriceServer.Worker;

public class CoinMarketTokenPriceSearchWorker : TokenPriceSearchWorkerBase
{
    private readonly TokenPriceSourceOption _option;
    private readonly ICoinMarketProvider _coinMarketProvider;
    protected override SourceType SourceType => SourceType.CoinMarket;

    public CoinMarketTokenPriceSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TokenPriceSourceOptions> options, ILogger<TokenPriceSearchWorkerBase> baseLogger,
        IPriceProvider priceProvider, ICoinMarketProvider coinMarketProvider) : base(timer, serviceScopeFactory,
        options, baseLogger, priceProvider)
    {
        _coinMarketProvider = coinMarketProvider;
        _option = options.Value.GetSourceOption(SourceType);
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        BaseLogger.LogInformation("[CoinMarket] Search worker Start...");

        await PriceProvider.UpdatePricesAsync(SourceType.CoinMarket,
            (await Task.WhenAll(_option.Tokens.Select(SearchTokenPriceAsync))).ToList());
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(CoinMarketTokenPriceSearchWorker),
        MethodName = nameof(HandleException))]
    public virtual async Task<PriceDto> SearchTokenPriceAsync(string tokenPair)
    {
        return new()
        {
            TokenPair = tokenPair,
            Price = await _coinMarketProvider.GetTokenPriceAsync(tokenPair),
            UpdateTime = DateTime.Now
        };
    }

    #region Exception handing

    public async Task<FlowBehavior> HandleException(Exception ex, string tokenPair)
    {
        switch (ex)
        {
            case HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests }:
                BaseLogger.LogWarning("[CoinMarket] Too Many Requests.");
                break;
            case HttpRequestException:
                if (ex.Message.Contains("Resource temporarily unavailable"))
                    BaseLogger.LogWarning("[CoinMarket] Resource temporarily unavailable.");
                break;
            case TaskCanceledException:
                BaseLogger.LogWarning("[CoinMarket] Operation timeout, need check the network.");
                break;
            default:
                BaseLogger.LogError(ex, $"[CoinMarket] Can not get {tokenPair} current price.");
                break;
        }

        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new PriceDto()
        };
    }

    #endregion
}