using System;
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

public class HamsterPriceSearchWorker : TokenPriceSearchWorkerBase
{
    private readonly TokenPriceSourceOption _option;
    private readonly IHamsterProvider _hamsterProvider;
    protected override SourceType SourceType => SourceType.Hamster;

    public HamsterPriceSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TokenPriceSourceOptions> options, ILogger<TokenPriceSearchWorkerBase> baseLogger,
        IPriceProvider priceProvider, IHamsterProvider hamsterProvider) : base(timer, serviceScopeFactory, options,
        baseLogger, priceProvider)
    {
        _hamsterProvider = hamsterProvider;
        _option = options.Value.GetSourceOption(SourceType);
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        BaseLogger.LogInformation("[Hamster] Search worker Start...");

        try
        {
            await PriceProvider.UpdatePricesAsync(SourceType.Hamster,
                await _hamsterProvider.GetTokenPriceAsync());
        }
        catch (Exception e)
        {
            BaseLogger.LogError(e, "[Hamster] Can not get acorns price.");
        }
    }
}