using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using AElf.CSharp.Core;
using AetherLink.Worker.Core.Constants;
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
    private readonly ConcurrentDictionary<string, long> _unconfirmedHeightMap = new();
    private readonly ConcurrentDictionary<string, int> _heightCompensationMap = new();

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

        // search Unconfirmed vrf job
        await SearchUnconfirmedJob(info);

        var blockLatestHeight = await _provider.GetBlockLatestHeightAsync(chainId);
        var startHeight = _heightMap[chainId];
        if (blockLatestHeight <= startHeight)
        {
            _logger.LogDebug(
                "[Search] {chain} startHeight is {latest} confirmed height is {latest}, height hasn't been updated yet, will try later.",
                chainId, startHeight, blockLatestHeight);
            return;
        }

        startHeight += 1;

        _logger.LogDebug("[Search] {chainId} startHeight: {s}, targetHeight:{t}.", chainId, startHeight,
            blockLatestHeight);
        var startTime = DateTime.Now;

        await Task.WhenAll(
            ExecuteJobsAsync(chainId, blockLatestHeight, startHeight),
            ExecuteTransmittedAsync(chainId, blockLatestHeight, startHeight),
            ExecuteRequestCanceledAsync(chainId, blockLatestHeight, startHeight)
        );

        _logger.LogDebug("[Search] {chain} search log took {time} ms.", chainId,
            DateTime.Now.Subtract(startTime).TotalMilliseconds);

        _heightMap[chainId] = blockLatestHeight;
        await _provider.SetLatestSearchHeightAsync(chainId, blockLatestHeight);
    }

    private async Task SearchUnconfirmedJob(ChainInfo info)
    {
        var chainId = info.ChainId;
        var startHeight = _unconfirmedHeightMap[chainId].Add(1);
        var maxHeight = startHeight;
        var batchSize = _options.UnconfirmedLogBatchSize.Add(_heightCompensationMap[chainId]);

        _logger.LogDebug("[UnconfirmedSearch] {chain} start:{s}, target:{t}.", chainId,
            startHeight,
            startHeight + batchSize);

        var startTime = DateTime.Now;
        var jobsCount = 0;
        var jobs = await _provider.SearchJobsAsync(chainId, startHeight.Add(batchSize), startHeight);
        foreach (var job in jobs.Where(job => job.RequestTypeIndex == RequestTypeConst.Vrf))
        {
            jobsCount = jobsCount.Add(1);
            maxHeight = Math.Max(job.BlockHeight, maxHeight);
            await _provider.HandleJobAsync(job);
        }

        _logger.LogDebug(
            "[UnconfirmedSearch] {chain} found {count} vrf jobs took {time} ms.",
            chainId,
            jobsCount, DateTime.Now.Subtract(startTime).TotalMilliseconds);

        // If there are no new events in this interval, the starting position will not be updated, but the search length will be updated.
        _unconfirmedHeightMap[chainId] = maxHeight == startHeight ? maxHeight - 1 : maxHeight;
        _heightCompensationMap[chainId] = maxHeight == startHeight ? batchSize : 0;

        _logger.LogDebug("[UnconfirmedSearch] {chain} height Compensation: {compensation}.", chainId,
            _heightCompensationMap[chainId]);

        await _provider.SetLatestUnconfirmedHeightAsync(chainId, _unconfirmedHeightMap[chainId]);
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

        var unconfirmedHeight = await _provider.GetUnconfirmedStartHeightAsync(chainId);
        _unconfirmedHeightMap[chainId] = unconfirmedHeight != 0
            // If after restarting, the unconfirmed height is less than the confirmed height,
            // it proves that there is indeed no data in the height compensation range before restarting.
            // so the confirmed height and unconfirmed height are aligned.
            ? unconfirmedHeight < _heightMap[chainId]
                ? _heightMap[chainId]
                : unconfirmedHeight
            : _heightMap[chainId];

        _heightCompensationMap[chainId] = 0;
    }

    // search oracle request start event
    private async Task ExecuteJobsAsync(string chainId, long to, long from)
    {
        var jobs = await _provider.SearchJobsAsync(chainId, to, from);
        var tasks = jobs.Select(job => _provider.HandleJobAsync(job));

        _logger.LogDebug("[Search] {chain} found a total of {count} jobs.", chainId, jobs.Count);

        await Task.WhenAll(tasks);
    }

    // search oracle transmitted event
    private async Task ExecuteTransmittedAsync(string chainId, long to, long from)
    {
        var transmits = await _provider.SearchTransmittedAsync(chainId, to, from);
        var transmittedTasks =
            transmits.Select(transmitted => _provider.HandleTransmittedLogEventAsync(transmitted));

        _logger.LogDebug("[Search] {chain} found a total of {count} transmitted.", chainId, transmits.Count);

        await Task.WhenAll(transmittedTasks);
    }

    // search oracle cancelled event
    private async Task ExecuteRequestCanceledAsync(string chainId, long to, long from)
    {
        var cancels = await _provider.SearchRequestCanceledAsync(chainId, to, from);
        var requestCancelsTasks = cancels.Select(requestCancelled =>
            _provider.HandleRequestCancelledLogEventAsync(requestCancelled));

        _logger.LogDebug("[Search] {chain} found a total of {count} canceled.", chainId, cancels.Count);

        await Task.WhenAll(requestCancelsTasks);
    }
}