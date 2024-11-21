using AetherLink.Server.Grains.Grain.Indexer;
using AetherLink.Server.HttpApi.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherLink.Server.HttpApi.Worker.AELF;

public class ConfirmBlockHeightSearchWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly AELFOptions _options;
    private readonly IClusterClient _clusterClient;

    public ConfirmBlockHeightSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IClusterClient clusterClient, IOptionsSnapshot<AELFOptions> options) : base(timer, serviceScopeFactory)
    {
        _options = options.Value;
        _clusterClient = clusterClient;
        timer.Period = _options.ConfirmBlockHeightTimer;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var client = _clusterClient.GetGrain<IAeFinderGrain>("confirmBlockHeight");
        await client.UpdateConfirmBlockHeightAsync();
    }
}