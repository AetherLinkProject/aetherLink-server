using System;
using System.Linq;
using System.Net.Http;
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

public class CoinBaseTokenPriceSearchWorker : TokenPriceSearchWorkerBase
{
    private readonly TokenPriceSourceOption _option;
    private readonly ICoinBaseProvider _coinbaseProvider;
    protected override SourceType SourceType => SourceType.CoinBase;

    public CoinBaseTokenPriceSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TokenPriceSourceOptions> options, ILogger<TokenPriceSearchWorkerBase> baseLogger,
        IPriceProvider priceProvider, ICoinBaseProvider coinbaseProvider) : base(timer, serviceScopeFactory, options,
        baseLogger, priceProvider)
    {
        _coinbaseProvider = coinbaseProvider;
        _option = options.Value.GetSourceOption(SourceType);
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        BaseLogger.LogInformation("[Coinbase] Search worker Start...");

        await PriceProvider.UpdatePricesAsync(SourceType.CoinBase,
            (await Task.WhenAll(_option.Tokens.Select(SearchTokenPriceAsync))).ToList());
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(CoinBaseTokenPriceSearchWorker),
        MethodName = nameof(HandleException))]
    public virtual async Task<PriceDto> SearchTokenPriceAsync(string tokenPair)
    {
        return new()
        {
            TokenPair = tokenPair,
            Price = await _coinbaseProvider.GetTokenPriceAsync(tokenPair),
            UpdateTime = DateTime.Now
        };
    }

    #region Exception handing

    public async Task<FlowBehavior> HandleException(Exception ex, string tokenPair)
    {
        if (ex is TaskCanceledException)
        {
            BaseLogger.LogWarning("[Coinbase] Timeout of 100 seconds elapsing.");
        }
        else if (ex is HttpRequestException he)
        {
            if (he.Message.Contains("Network is unreachable"))
            {
                BaseLogger.LogError($"[Coinbase] Please check the network.");
            }
        }
        else
        {
            BaseLogger.LogError(ex, $"[Coinbase] Can not get {tokenPair} current price.");
        }

        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new PriceDto()
        };
    }

    #endregion
}