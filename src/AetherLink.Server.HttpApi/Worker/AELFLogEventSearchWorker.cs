using AetherLink.Server.Grains.Grain.Request;
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
    private readonly IClusterClient _clusterClient;

    public AELFLogEventSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<LogEventSearchOptions> options, ILogger<LogEventSearchWorkerBase> baseLogger,
        IClusterClient clusterClient) : base(timer, serviceScopeFactory, options, baseLogger)
    {
        _clusterClient = clusterClient;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        BaseLogger.LogDebug("AELFLogEventSearchWorker");
        var orderGrain = _clusterClient.GetGrain<ICrossChainRequestGrain>("9992731-123");
        var result = await orderGrain.UpdateAsync(new()
        {
            Id = "9992731-123",
            SourceChainId = 1,
            TargetChainId = 1100,
            MessageId = "messageId",
            Status = CrossChainStatus.Committed.ToString()
        });

        BaseLogger.LogDebug($"AELF update 9992731-123 {result.Success}");
    }

    protected override ChainType ChainType => ChainType.AELF;
}