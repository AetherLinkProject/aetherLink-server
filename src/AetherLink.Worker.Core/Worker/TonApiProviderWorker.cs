using System.Threading.Tasks;
using AetherLink.Worker.Core.Common.TonIndexer;
using AetherLink.Worker.Core.Constants;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherLink.Worker.Core.Worker;

public class TonApiProviderWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly TonIndexerRouter _tonIndexerRouter;

    public TonApiProviderWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        TonIndexerRouter tonIndexerRouter) : base(timer,
        serviceScopeFactory)
    {
        _tonIndexerRouter = tonIndexerRouter;

        timer.Period = 1000 * TonEnvConstants.ApiProviderHealthCheckPeriod;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        // api provider health check
        var apiProviderList = _tonIndexerRouter.GetIndexerApiProviderList();
        foreach (var provider in apiProviderList)
        {
            var needCheckAvailable = await provider.NeedCheckConnection();
            if (needCheckAvailable)
            {
                await provider.CheckConnection();
            }
        }
    }
}