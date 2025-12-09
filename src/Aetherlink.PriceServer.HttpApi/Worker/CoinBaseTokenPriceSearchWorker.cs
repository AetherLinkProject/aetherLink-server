using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
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

    private async Task<PriceDto> SearchTokenPriceAsync(string tokenPair)
    {
        try
        {
            return new()
            {
                TokenPair = tokenPair,
                Price = await _coinbaseProvider.GetTokenPriceAsync(tokenPair),
                UpdateTime = DateTime.Now
            };
        }
        catch (TaskCanceledException)
        {
            BaseLogger.LogWarning("[Coinbase] Timeout of 100 seconds elapsing.");
            return new();
        }
        catch (HttpRequestException he)
        {
            if (he.Message.Contains("Network is unreachable"))
            {
                BaseLogger.LogError($"[Coinbase] Please check the network.");
            }

            return new();
        }
        catch (Exception e)
        {
            BaseLogger.LogError(e, $"[Coinbase] Can not get {tokenPair} current price.");
            return new();
        }
    }
}