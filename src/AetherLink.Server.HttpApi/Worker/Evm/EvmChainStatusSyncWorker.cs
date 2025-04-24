using AetherLink.Server.Grains.Grain.Indexer;
using AetherLink.Server.HttpApi.Constants;
using AetherLink.Server.HttpApi.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherLink.Server.HttpApi.Worker.Evm;

public class EvmChainStatusSyncWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly EVMOptions _options;
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<EvmChainStatusSyncWorker> _logger;

    public EvmChainStatusSyncWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IClusterClient clusterClient, IOptionsSnapshot<EVMOptions> options, ILogger<EvmChainStatusSyncWorker> logger) :
        base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _clusterClient = clusterClient;
        timer.Period = _options.DelayTransactionSearchTimer;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.LogDebug("[EvmChainStatusSyncWorker] Start ...");

        await _clusterClient.GetGrain<IEvmGrain>(GrainKeyConstants.ConfirmBlockHeightGrainKey)
            .UpdateLatestBlockHeightAsync();
    }
}