using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using AElf.CSharp.Core;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherLink.Worker.Core.Worker;

public class LogsPoller : AsyncPeriodicBackgroundWorkerBase
{
    private readonly WorkerOptions _options;
    private readonly ILogger<LogsPoller> _logger;
    private readonly ILogPollerProvider _provider;
    private readonly IAeFinderProvider _aeFinderProvider;

    private readonly ConcurrentDictionary<string, long> _lastLookBackBlockHeightMap = new();

    public LogsPoller(AbpAsyncTimer timer, ILogPollerProvider provider, IOptions<WorkerOptions> workerOptions,
        IServiceScopeFactory serviceScopeFactory, ILogger<LogsPoller> logger, IAeFinderProvider aeFinderProvider) :
        base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _provider = provider;
        _options = workerOptions.Value;
        Timer.Period = _options.PollerTimer;
        _aeFinderProvider = aeFinderProvider;
        Initialize().GetAwaiter().GetResult();
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
        => await Task.WhenAll((await _aeFinderProvider.GetChainSyncStateAsync()).Select(c =>
            ProcessAsync(c.ChainId, c.LastIrreversibleBlockHeight)));

    private async Task ProcessAsync(string chainId, long confirmedHeight)
    {
        var lastLookBackBlockHeight = _lastLookBackBlockHeightMap[chainId];
        if (confirmedHeight <= lastLookBackBlockHeight)
        {
            _logger.LogDebug("[LogsPoller] {chain} {latest} <= {last}, will try to pull the log later.",
                chainId, lastLookBackBlockHeight, confirmedHeight);
            return;
        }

        var startTime = DateTime.Now;
        var jobs = await _provider.PollLogEventsAsync(chainId, confirmedHeight,
            lastLookBackBlockHeight.Add(1));
        _logger.LogDebug("[LogsPoller] {chain} found a total of {count} event.", chainId, jobs.Count);

        await Task.WhenAll(jobs.Select(job => _provider.HandlerEventAsync(job)));

        _logger.LogDebug("[LogsPoller] {chain} log poll took {time} ms.", chainId,
            DateTime.Now.Subtract(startTime).TotalMilliseconds);

        _lastLookBackBlockHeightMap[chainId] = confirmedHeight;
        await _provider.SetLookBackBlockHeightAsync(chainId, confirmedHeight);
    }

    private async Task Initialize()
    {
        _logger.LogInformation("[LogsPoller] AetherLink node LogsPoller initializing.");
        var chainStates = await _aeFinderProvider.GetChainSyncStateAsync();
        foreach (var chain in chainStates)
        {
            var chainId = chain.ChainId;
            var redisHeight = await _provider.GetLookBackBlockHeightAsync(chainId);
            _lastLookBackBlockHeightMap[chainId] = redisHeight.BlockHeight != 0
                ? redisHeight.BlockHeight
                : chain.LastIrreversibleBlockHeight;
        }
    }
}