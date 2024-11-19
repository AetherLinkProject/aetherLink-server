using AetherLink.Server.HttpApi.Dtos;
using AetherLink.Server.HttpApi.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherLink.Server.HttpApi.Worker;

public class TonLogEventSearchWorker : LogEventSearchWorkerBase
{
    protected override ChainType ChainType => ChainType.TON;
    public TonLogEventSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<LogEventSearchOptions> options, ILogger<LogEventSearchWorkerBase> baseLogger) : base(timer,
        serviceScopeFactory, options, baseLogger)
    {
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        BaseLogger.LogDebug("TonLogEventSearchWorker");
    }

}