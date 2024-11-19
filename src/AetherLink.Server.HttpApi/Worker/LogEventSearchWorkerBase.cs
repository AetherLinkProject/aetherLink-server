using AetherLink.Server.HttpApi.Dtos;
using AetherLink.Server.HttpApi.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherLink.Server.HttpApi.Worker;

public abstract class LogEventSearchWorkerBase : AsyncPeriodicBackgroundWorkerBase
{
    // protected readonly IPriceProvider PriceProvider;
    protected abstract ChainType ChainType { get; }
    protected readonly ILogger<LogEventSearchWorkerBase> BaseLogger;
    
    protected LogEventSearchWorkerBase(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<LogEventSearchOptions> options, ILogger<LogEventSearchWorkerBase> baseLogger) : base(timer,
        serviceScopeFactory)
    {
        BaseLogger = baseLogger;
        timer.Period = options.Value.GetSourceOption(ChainType).Interval;
    }
}