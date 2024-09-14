using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using AElf.CSharp.Core;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Reporter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherLink.Worker.Core.Worker;

public class UnconfirmedWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly WorkerOptions _options;
    private readonly IWorkerReporter _reporter;
    private readonly IWorkerProvider _provider;
    private readonly ILogger<UnconfirmedWorker> _logger;
    private readonly IAeFinderProvider _aeFinderProvider;
    private readonly ConcurrentDictionary<string, long> _unconfirmedHeightMap = new();

    public UnconfirmedWorker(AbpAsyncTimer timer, IOptionsSnapshot<WorkerOptions> workerOptions,
        IWorkerProvider provider, IServiceScopeFactory serviceScopeFactory, ILogger<UnconfirmedWorker> logger,
        IWorkerReporter reporter, AeFinderProvider aeFinderProvider) : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _reporter = reporter;
        _provider = provider;
        _options = workerOptions.Value;
        _aeFinderProvider = aeFinderProvider;
        Timer.Period = _options.UnconfirmedTimer;
        Initialize().GetAwaiter().GetResult();
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
        => await Task.WhenAll((await _aeFinderProvider.GetChainSyncStateAsync()).Select(c =>
            ProcessAsync(c.ChainId, c.BestChainHeight, c.LastIrreversibleBlockHeight)));

    private async Task ProcessAsync(string chainId, long bestChainHeight, long libHeight)
    {
        var startHeight = _unconfirmedHeightMap[chainId];
        if (bestChainHeight <= startHeight) return;

        startHeight = startHeight < libHeight ? libHeight : startHeight.Add(1);

        var maxHeight = startHeight;
        var jobs = await _provider.SearchJobsAsync(chainId, bestChainHeight, startHeight);
        foreach (var job in jobs.Where(job => job.RequestTypeIndex == RequestTypeConst.Vrf))
        {
            maxHeight = Math.Max(job.BlockHeight, maxHeight);
            await _provider.HandleJobAsync(job);
        }

        _reporter.RecordUnconfirmedBlockHeight(chainId, startHeight, maxHeight);
        _unconfirmedHeightMap[chainId] = maxHeight == startHeight ? maxHeight - 1 : maxHeight;
        await _provider.SetLatestUnconfirmedHeightAsync(chainId, _unconfirmedHeightMap[chainId]);

        _logger.LogInformation(
            $"[Unconfirmed] The unconfirmed worker has processed up to block height {bestChainHeight}, and LIB is at block height {libHeight}.");
    }

    private async Task Initialize()
    {
        _logger.LogInformation("[Unconfirmed] AetherLink node Unconfirmed Worker initializing.");
        var chainStates = await _aeFinderProvider.GetChainSyncStateAsync();
        foreach (var chain in chainStates)
        {
            var chainId = chain.ChainId;
            var redisHeight = await _provider.GetUnconfirmedStartHeightAsync(chainId);
            _unconfirmedHeightMap[chainId] = redisHeight != 0
                ? redisHeight
                : chain.BestChainHeight;
        }
    }
}