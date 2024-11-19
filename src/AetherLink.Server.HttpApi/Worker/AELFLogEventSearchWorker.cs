using AetherLink.Server.HttpApi.Dtos;
using AetherLink.Server.HttpApi.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherLink.Server.HttpApi.Worker;

public class AELFLogEventSearchWorker : LogEventSearchWorkerBase
{
    public AELFLogEventSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<LogEventSearchOptions> options, ILogger<LogEventSearchWorkerBase> baseLogger) : base(timer,
        serviceScopeFactory, options, baseLogger)
    {
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        BaseLogger.LogDebug("AELFLogEventSearchWorker");
    }

    protected override ChainType ChainType => ChainType.AELF;
}