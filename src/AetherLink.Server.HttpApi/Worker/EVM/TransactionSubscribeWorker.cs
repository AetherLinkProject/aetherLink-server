using AetherLink.Server.Grains.Grain.Indexer;
using AetherLink.Server.HttpApi.Constants;
using AetherLink.Server.HttpApi.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherLink.Server.HttpApi.Worker.EVM;

public class TransactionSubscribeWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly EVMOptions _options;
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<TransactionSubscribeWorker> _logger;

    public TransactionSubscribeWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<EVMOptions> options, IClusterClient clusterClient, ILogger<TransactionSubscribeWorker> logger)
        : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _clusterClient = clusterClient;
        timer.Period = _options.TransactionSearchTimer;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var client = _clusterClient.GetGrain<ITonIndexerGrain>(GrainKeyConstants.SubscribeTransactionGrainKey);
        var result = await client.SearchTonTransactionsAsync();

        if (!result.Success) return;
        // await Task.WhenAll(result.Data.Select(HandlerEvmTransactionAsync));
    }

    private async Task HandlerEvmTransactionAsync()
    {
    }
}