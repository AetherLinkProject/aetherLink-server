using System;
using System.Linq;
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

public class OkxTokenPriceSearchWorker : TokenPriceSearchWorkerBase
{
    private readonly IOkxProvider _okxProvider;
    private readonly TokenPriceSourceOption _option;
    protected override SourceType SourceType => SourceType.Okx;

    public OkxTokenPriceSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TokenPriceSourceOptions> options, ILogger<TokenPriceSearchWorkerBase> baseLogger,
        IPriceProvider priceProvider, IOkxProvider okxProvider) : base(timer, serviceScopeFactory, options, baseLogger,
        priceProvider)
    {
        _okxProvider = okxProvider;
        _option = options.Value.GetSourceOption(SourceType);
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        BaseLogger.LogInformation("[OKX] Search worker Start...");

        await PriceProvider.UpdatePricesAsync(SourceType.Okx,
            (await Task.WhenAll(_option.Tokens.Select(SearchTokenPriceAsync))).ToList());
    }

    private async Task<PriceDto> SearchTokenPriceAsync(string tokenPair)
    {
        try
        {
            return new()
            {
                TokenPair = tokenPair,
                Price = await _okxProvider.GetTokenPriceAsync(tokenPair),
                UpdateTime = DateTime.Now
            };
        }
        catch (Exception e)
        {
            BaseLogger.LogError(e, $"[OKX] Can not get {tokenPair} current price.");
            return new();
        }
    }
}