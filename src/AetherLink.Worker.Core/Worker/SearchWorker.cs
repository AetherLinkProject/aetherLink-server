using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherLink.Worker.Core.Worker;

public class SearchWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly WorkerOptions _options;
    private readonly IWorkerProvider _provider;
    private readonly ILogger<SearchWorker> _logger;
    private readonly ConcurrentDictionary<string, long> _heightMap = new();

    public SearchWorker(AbpAsyncTimer timer, IOptionsSnapshot<WorkerOptions> workerOptions, IWorkerProvider provider,
        IServiceScopeFactory serviceScopeFactory, ILogger<SearchWorker> logger) : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _provider = provider;
        _options = workerOptions.Value;
        Timer.Period = 1000 * _options.SearchTimer;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var tasks = _options.Chains.Select(ExecuteSearchAsync);
        await Task.WhenAll(tasks);
    }

    private async Task ExecuteSearchAsync(ChainInfo info)
    {
        var chainId = info.ChainId;

        // when restart _heightMap is empty
        if (!_heightMap.TryGetValue(chainId, out _)) await SearchWorkerInitializing(info);

        var blockLatestHeight = await _provider.GetBlockLatestHeightAsync(chainId);
        var startHeight = _heightMap[chainId];
        if (blockLatestHeight <= startHeight) return;
        // _logger.LogDebug("[Search] {chain} ConfirmHeight hasn't been updated yet, will try later.", chainId);

        startHeight += 1;
        var jobsCount = 0;
        var transmittedCount = 0;
        var requestCanceledCount = 0;
        var batchSize = _options.LogBackFillBatchSize;

        var startTime = DateTime.Now;
        for (var from = startHeight; from <= blockLatestHeight; from += batchSize)
        {
            var to = from + batchSize - 1;
            if (to > blockLatestHeight) to = blockLatestHeight;

            jobsCount += await ExecuteJobsAsync(chainId, to, from);
            transmittedCount += await ExecuteTransmittedAsync(chainId, to, from);
            requestCanceledCount += await ExecuteRequestCanceledAsync(chainId, to, from);
        }

        _logger.LogDebug(
            "[Search] {chainId} startHeight: {s}, targetHeight:{t} found a total of {jobs} jobs, {transmit} transmitted, {canceled} canceled and took {time} ms.",
            chainId, startHeight, blockLatestHeight, jobsCount, transmittedCount, requestCanceledCount,
            DateTime.Now.Subtract(startTime).TotalMilliseconds);

        _heightMap[chainId] = blockLatestHeight;
        await _provider.SetLatestSearchHeightAsync(chainId, blockLatestHeight);
    }

    private async Task SearchWorkerInitializing(ChainInfo info)
    {
        var chainId = info.ChainId;
        _logger.LogInformation("[Search] Aetherlink node SearchWorker initializing in {chain}.", chainId);

        // check redis latest height
        var redisHeight = await _provider.GetStartHeightAsync(chainId);
        _heightMap[chainId] = redisHeight != 0
            ? redisHeight
            // will start at block LatestHeight when info.LatestHeight == -1, and (continue) skip this round.
            : info.LatestHeight == -1
                ? await _provider.GetBlockLatestHeightAsync(chainId)
                : info.LatestHeight;
    }

    // search oracle request start event
    private async Task<int> ExecuteJobsAsync(string chainId, long to, long from)
    {
        var jobs = await _provider.SearchJobsAsync(chainId, to, from);
        var tasks = jobs.Select(job => _provider.HandleJobAsync(job));
        await Task.WhenAll(tasks);
        return jobs.Count;
    }

    // search oracle transmitted event
    private async Task<int> ExecuteTransmittedAsync(string chainId, long to, long from)
    {
        var transmits = await _provider.SearchTransmittedAsync(chainId, to, from);
        var transmittedTasks =
            transmits.Select(transmitted => _provider.HandleTransmittedLogEventAsync(transmitted));
        await Task.WhenAll(transmittedTasks);
        return transmits.Count;
    }

    // search oracle cancelled event
    private async Task<int> ExecuteRequestCanceledAsync(string chainId, long to, long from)
    {
        var requestCancels = await _provider.SearchRequestCanceledAsync(chainId, to, from);
        var requestCancelsTasks = requestCancels.Select(requestCancelled =>
            _provider.HandleRequestCancelledLogEventAsync(requestCancelled));
        await Task.WhenAll(requestCancelsTasks);
        return requestCancels.Count;
    }
}