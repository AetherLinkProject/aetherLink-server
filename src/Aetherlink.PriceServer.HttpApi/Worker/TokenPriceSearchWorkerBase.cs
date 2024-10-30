using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Options;
using AetherlinkPriceServer.Provider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherlinkPriceServer.Worker;

public abstract class TokenPriceSearchWorkerBase : AsyncPeriodicBackgroundWorkerBase
{
    protected readonly IPriceProvider PriceProvider;
    protected abstract SourceType SourceType { get; }

    protected TokenPriceSearchWorkerBase(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TokenPriceSourceOptions> options, IPriceProvider priceProvider) : base(timer,
        serviceScopeFactory)
    {
        PriceProvider = priceProvider;
        timer.Period = options.Value.GetSourceOption(SourceType).Interval;
    }
}