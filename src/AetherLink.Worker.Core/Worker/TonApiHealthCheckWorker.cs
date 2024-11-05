using System.Collections.Generic;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider.TonIndexer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherLink.Worker.Core.Worker;

public class TonApiHealthCheckWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly TonApiHealthCheckOptions _options;
    private readonly TonIndexerRouter _tonIndexerRouter;
    private readonly ILogger<TonApiHealthCheckWorker> _logger;

    public TonApiHealthCheckWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        TonIndexerRouter tonIndexerRouter, IOptionsSnapshot<TonApiHealthCheckOptions> options,
        ILogger<TonApiHealthCheckWorker> logger) : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _tonIndexerRouter = tonIndexerRouter;
        timer.Period = _options.HealthCheckInterval;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.LogDebug("[TonApiHealthCheckWorker] Start checking Ton API Connection...");
        var taskList = new List<Task>();
        foreach (var provider in _tonIndexerRouter.GetIndexerApiProviderList())
        {
            var needCheckAvailable = await provider.NeedCheckConnection();
            if (!needCheckAvailable) continue;
            taskList.Add(provider.CheckConnection());
        }

        await Task.WhenAll(taskList);
    }
}