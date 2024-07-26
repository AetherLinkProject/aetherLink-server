using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using AElf.CSharp.Core;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Worker.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherLink.Worker.Core.Worker;

public class LogPoller : AsyncPeriodicBackgroundWorkerBase
{
    // private readonly IWorkerReporter _reporter;
    private readonly ILogPollerProvider _provider;
    private readonly WorkerOptions _workerOptions;
    private readonly ILogger<LogPoller> _logger;
    private readonly ConcurrentDictionary<string, long> _lastLookBackBlockHeightMap = new();

    public LogPoller(AbpAsyncTimer timer, IOptions<LogPollerOptions> pollerOptions, ILogPollerProvider provider,
        IOptions<WorkerOptions> workerOptions, IServiceScopeFactory serviceScopeFactory, ILogger<LogPoller> logger) :
        base(timer, serviceScopeFactory)
    {
        // _reporter = reporter;
        _logger = logger;
        _provider = provider;
        _workerOptions = workerOptions.Value;
        Timer.Period = pollerOptions.Value.PollerTimer;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
        => await Task.WhenAll(_workerOptions.Chains.Select(ExecuteSearchAsync));

    private async Task ExecuteSearchAsync(ChainInfo info)
    {
        var chainId = info.ChainId;
        var latestConfirmBlockHeight = await _provider.GetConfirmedBlockHeightAsync(chainId);

        if (!_lastLookBackBlockHeightMap.TryGetValue(chainId, out _))
        {
            _logger.LogInformation("[LogPoller] Aetherlink node start {chain} initializing.", chainId);

            var redisHeight = await _provider.GetLookBackBlockHeightAsync(chainId);
            _lastLookBackBlockHeightMap[chainId] = redisHeight != null && redisHeight.BlockHeight != 0
                ? redisHeight.BlockHeight
                : info.LatestHeight == -1
                    ? latestConfirmBlockHeight
                    : info.LatestHeight;
        }

        var lastLookBackBlockHeight = _lastLookBackBlockHeightMap[chainId];
        if (latestConfirmBlockHeight <= lastLookBackBlockHeight)
        {
            _logger.LogDebug("[LogPoller] {chain} {latest} <= {last}, will try to pull the log later.",
                chainId, lastLookBackBlockHeight, latestConfirmBlockHeight);
            return;
        }

        var startTime = DateTime.Now;
        var jobs = await _provider.PollLogEventsAsync(chainId, latestConfirmBlockHeight,
            lastLookBackBlockHeight.Add(1));
        _logger.LogDebug("[LogPoller] {chain} found a total of {count} event.", chainId, jobs.Count);

        await Task.WhenAll(jobs.Select(job => _provider.HandlerEventAsync(job)));

        _logger.LogDebug("[LogPoller] {chain} log poll took {time} ms.", chainId,
            DateTime.Now.Subtract(startTime).TotalMilliseconds);

        _lastLookBackBlockHeightMap[chainId] = latestConfirmBlockHeight;
        await _provider.SetLookBackBlockHeightAsync(chainId, latestConfirmBlockHeight);

        // todo: Add metrics
    }
}