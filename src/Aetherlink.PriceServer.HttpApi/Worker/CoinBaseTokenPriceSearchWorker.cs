using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Options;
using AetherlinkPriceServer.Provider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using Serilog;

namespace AetherlinkPriceServer.Worker;

public class CoinBaseTokenPriceSearchWorker : TokenPriceSearchWorkerBase
{
    private readonly ILogger _logger;
    private readonly TokenPriceSourceOption _option;
    private readonly ICoinBaseProvider _coinbaseProvider;
    protected override SourceType SourceType => SourceType.CoinBase;

    public CoinBaseTokenPriceSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TokenPriceSourceOptions> options, IPriceProvider priceProvider,
        ICoinBaseProvider coinbaseProvider) : base(timer, serviceScopeFactory, options, priceProvider)
    {
        _coinbaseProvider = coinbaseProvider;
        _option = options.Value.GetSourceOption(SourceType);
        _logger = Log.ForContext<CoinBaseTokenPriceSearchWorker>();
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.Information("[Coinbase] Search worker Start...");

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
            _logger.Warning("[Coinbase] Timeout of 100 seconds elapsing.");
            return new();
        }
        catch (HttpRequestException he)
        {
            if (he.Message.Contains("Network is unreachable"))
            {
                _logger.Error($"[Coinbase] Please check the network.");
            }

            return new();
        }
        catch (Exception e)
        {
            _logger.Error(e, $"[Coinbase] Can not get {tokenPair} current price.");
            return new();
        }
    }
}