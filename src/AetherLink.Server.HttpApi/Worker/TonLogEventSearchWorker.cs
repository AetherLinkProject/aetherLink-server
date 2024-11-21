using AetherLink.Server.Grains.Grain.Request;
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
    private readonly IClusterClient _clusterClient;

    protected override ChainType ChainType => ChainType.TON;

    public TonLogEventSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<LogEventSearchOptions> options, ILogger<LogEventSearchWorkerBase> baseLogger,
        IClusterClient clusterClient) : base(timer,
        serviceScopeFactory, options, baseLogger)
    {
        _clusterClient = clusterClient;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        BaseLogger.LogDebug("TonLogEventSearchWorker");
        var orderGrain = _clusterClient.GetGrain<ICrossChainRequestGrain>("1100-123");
        var result = await orderGrain.UpdateAsync(new()
        {
            Id = "1100-123",
            SourceChainId = 1100,
            TargetChainId = 1,
            MessageId = "messageId",
            Status = CrossChainStatus.Started.ToString()
        });

        BaseLogger.LogDebug($"Ton update 1100-123 {result.Success}");
    }
}