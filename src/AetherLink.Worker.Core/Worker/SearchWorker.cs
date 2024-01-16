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
    private ConcurrentDictionary<string, long> _heightMap = new();

    public SearchWorker(AbpAsyncTimer timer, IOptionsSnapshot<WorkerOptions> workerOptions,
        IServiceScopeFactory serviceScopeFactory, IWorkerProvider provider, ILogger<SearchWorker> logger) : base(timer,
        serviceScopeFactory)
    {
        _provider = provider;
        _logger = logger;
        _options = workerOptions.Value;
        Timer.Period = 1000 * _options.SearchTimer;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.LogDebug("[SearchWorker] Executing.");
        foreach (var info in _options.Chains)
        {
            var chainId = info.ChainId;
            // when restart _heightMap is empty
            if (!_heightMap.TryGetValue(chainId, out _))
            {
                _logger.LogInformation("[SearchWorker] heightMap not set.");
                // will start at block LatestHeight when info.LatestHeight == -1, and (continue) skip this round.
                if (info.LatestHeight == -1)
                {
                    var latestHeight = await _provider.GetBlockLatestHeightAsync(chainId);
                    _heightMap[chainId] = latestHeight;
                    continue;
                }

                // check redis latest height
                var redisHeight = await _provider.GetStartHeightAsync(chainId);
                _heightMap[chainId] = redisHeight == 0 ? info.LatestHeight : redisHeight;
            }

            var blockLatestHeight = await _provider.GetBlockLatestHeightAsync(chainId);
            var startHeight = _heightMap[chainId];
            if (blockLatestHeight == startHeight)
            {
                _logger.LogDebug("[SearchWorker] blockLatestHeight equal startHeight, no need search.");
                continue;
            }

            // Overflow protection: It's no mean to search block which long time ago(over 1000 height)
            var fromBlock = blockLatestHeight - _options.BlockBackFillDepth;
            if (fromBlock > startHeight)
            {
                _logger.LogWarning("[SearchWorker] Overflow protection.");
                startHeight = fromBlock;
            }

            var batchSize = _options.LogBackFillBatchSize;

            startHeight += 1;
            _logger.LogDebug("[SearchWorker] {chain} startHeight: {s}, targetHeight:{t}", chainId, startHeight,
                blockLatestHeight);
            var jobsCount = 0;
            var startTime = DateTime.Now;
            for (var from = startHeight; from <= blockLatestHeight; from += batchSize)
            {
                var to = from + batchSize - 1;
                if (to > blockLatestHeight) to = blockLatestHeight;
                var jobs = await _provider.SearchJobsAsync(chainId, to, from);
                jobsCount += jobs.Count;
                var tasks = jobs.Select(job => _provider.HandleJobAsync(job));
                await Task.WhenAll(tasks);
            }

            _logger.LogInformation("[SearchWorker] This query found a total of {amount} jobs and took {time} ms.",
                jobsCount, DateTime.Now.Subtract(startTime).TotalMilliseconds);

            _heightMap[chainId] = blockLatestHeight;
            await _provider.SetLatestSearchHeightAsync(chainId, blockLatestHeight);
        }
    }
}