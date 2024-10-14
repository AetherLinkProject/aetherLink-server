using System;
using System.Collections.Generic;
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

public class CoinGeckoTokenPriceSearchWorker : TokenPriceSearchWorkerBase
{
    private readonly List<string> _coinIds;
    private readonly ICoinGeckoProvider _coinGeckoProvider;
    protected override SourceType SourceType => SourceType.CoinGecko;

    public CoinGeckoTokenPriceSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TokenPriceSourceOptions> options, ILogger<TokenPriceSearchWorkerBase> baseLogger,
        IPriceProvider priceProvider, ICoinGeckoProvider coinGeckoProvider) : base(timer, serviceScopeFactory, options,
        baseLogger, priceProvider)
    {
        _coinGeckoProvider = coinGeckoProvider;
        _coinIds = options.Value.GetSourceOption(SourceType).Tokens;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        BaseLogger.LogInformation("[CoinGecko] Search worker Start...");

        await CollectRealTimePricesAsync();
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(CoinGeckoTokenPriceSearchWorker),
        MethodName = nameof(HandleException))]
    public virtual async Task CollectRealTimePricesAsync()
    {
        await PriceProvider.UpdatePricesAsync(SourceType.CoinGecko,
            await _coinGeckoProvider.GetTokenPricesAsync(_coinIds));
    }

    #region Exception handing

    public async Task<FlowBehavior> HandleException(Exception ex)
    {
        if (ex is TaskCanceledException)
        {
            BaseLogger.LogWarning("[CoinGecko] Timeout of 100 seconds elapsing.");
        }
        else if (ex is HttpRequestException he)
        {
            if (he.StatusCode == HttpStatusCode.TooManyRequests)
            {
                BaseLogger.LogWarning("[CoinGecko] Too Many Requests");
                Thread.Sleep(10000);
            }
        }
        else
        {
            BaseLogger.LogError(ex, "[CoinGecko] Query token price error.");
        }

        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return
        };
    }

    #endregion
}