using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using AElf.CSharp.Core;
using AetherLink.Indexer.Provider;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Provider.SearcherProvider;
using AetherLink.Worker.Core.Reporter;
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
    private readonly IWorkerReporter _reporter;
    private readonly ILogger<SearchWorker> _logger;
    private readonly IAeFinderProvider _aeFinderProvider;
    private readonly ConcurrentDictionary<string, long> _heightMap = new();
    private readonly ICrossChainRequestProvider _crossChainRequestProvider;

    public SearchWorker(AbpAsyncTimer timer, IOptionsSnapshot<WorkerOptions> workerOptions, IWorkerProvider provider,
        IServiceScopeFactory serviceScopeFactory, ILogger<SearchWorker> logger, IWorkerReporter reporter,
        IAeFinderProvider aeFinderProvider, ICrossChainRequestProvider crossChainRequestProvider) : base(timer,
        serviceScopeFactory)
    {
        _logger = logger;
        _reporter = reporter;
        _provider = provider;
        _options = workerOptions.Value;
        Timer.Period = _options.SearchTimer;
        _aeFinderProvider = aeFinderProvider;
        _crossChainRequestProvider = crossChainRequestProvider;
        Initialize().GetAwaiter().GetResult();
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
        => await Task.WhenAll((await _aeFinderProvider.GetChainSyncStateAsync()).Select(c =>
            ExecuteSearchAsync(c.ChainId, c.LastIrreversibleBlockHeight)));

    private async Task ExecuteSearchAsync(string chainId, long blockLatestHeight)
    {
        var startHeight = _heightMap[chainId];
        if (blockLatestHeight <= startHeight) return;

        startHeight = startHeight.Add(1);

        _logger.LogDebug("[Search] {chainId} startHeight: {s}, targetHeight:{t}.", chainId, startHeight,
            blockLatestHeight);
        var startTime = DateTime.Now;

        await Task.WhenAll(
            // ExecuteJobsAsync(chainId, blockLatestHeight, startHeight),
            // ExecuteTransmittedAsync(chainId, blockLatestHeight, startHeight),
            // ExecuteRequestCanceledAsync(chainId, blockLatestHeight, startHeight),
            ExecuteRampRequestsAsync(chainId, blockLatestHeight, startHeight)
            // ExecuteRampManuallyExecutedAsync(chainId, blockLatestHeight, startHeight),
            // ExecuteRampRequestsCanceledAsync(chainId, blockLatestHeight, startHeight)
        );

        _logger.LogDebug("[Search] {chain} search log took {time} ms.", chainId,
            DateTime.Now.Subtract(startTime).TotalMilliseconds);

        _heightMap[chainId] = blockLatestHeight;
        _reporter.RecordConfirmBlockHeight(chainId, startHeight, blockLatestHeight);
        await _provider.SetLatestSearchHeightAsync(chainId, blockLatestHeight);
    }

    private async Task Initialize()
    {
        _logger.LogInformation("[Search] AetherLink node SearchWorker initializing.");
        var chainStates = await _aeFinderProvider.GetChainSyncStateAsync();
        foreach (var chain in chainStates)
        {
            var chainId = chain.ChainId;
            var redisHeight = await _provider.GetStartHeightAsync(chainId);
            _heightMap[chainId] = redisHeight != 0
                ? redisHeight
                : chain.LastIrreversibleBlockHeight;
        }
    }

    // search oracle request start event
    private async Task ExecuteJobsAsync(string chainId, long to, long from)
    {
        var jobs = await _provider.SearchJobsAsync(chainId, to, from);
        var tasks = jobs.Select(job => _provider.HandleJobAsync(job));
        _reporter.RecordOracleJobAsync(chainId, jobs.Count);

        _logger.LogDebug("[Search] {chain} found a total of {count} jobs.", chainId, jobs.Count);

        await Task.WhenAll(tasks);
    }

    private async Task ExecuteRampRequestsAsync(string chainId, long to, long from)
    {
        var requests = await _provider.SearchRampRequestsAsync(chainId, to, from);
        var tasks = requests.Select(r => _crossChainRequestProvider.StartCrossChainRequestFromAELf(r));

        _reporter.RecordRampJobAsync(chainId, requests.Count);
        _logger.LogDebug("[Search] {chain} found a total of {count} ramp requests.", chainId, requests.Count);

        await Task.WhenAll(tasks);
    }

    // search oracle transmitted event
    private async Task ExecuteTransmittedAsync(string chainId, long to, long from)
    {
        var transmits = await _provider.SearchTransmittedAsync(chainId, to, from);
        var transmittedTasks =
            transmits.Select(transmitted => _provider.HandleTransmittedLogEventAsync(transmitted));

        _reporter.RecordTransmittedAsync(chainId, transmits.Count);
        _logger.LogDebug("[Search] {chain} found a total of {count} transmitted.", chainId, transmits.Count);

        await Task.WhenAll(transmittedTasks);
    }

    // search oracle cancelled event
    private async Task ExecuteRequestCanceledAsync(string chainId, long to, long from)
    {
        var cancels = await _provider.SearchRequestCanceledAsync(chainId, to, from);
        var requestCancelsTasks = cancels.Select(requestCancelled =>
            _provider.HandleRequestCancelledLogEventAsync(requestCancelled));
        _reporter.RecordCanceledAsync(chainId, cancels.Count);

        _logger.LogDebug("[Search] {chain} found a total of {count} canceled.", chainId, cancels.Count);

        await Task.WhenAll(requestCancelsTasks);
    }

    // search ramp cancelled event
    private async Task ExecuteRampRequestsCanceledAsync(string chainId, long to, long from)
    {
        var cancels = await _provider.SearchRampRequestCanceledAsync(chainId, to, from);
        var requestCancelsTasks = cancels.Select(requestCancelled =>
            _provider.HandleRampRequestCancelledLogEventAsync(requestCancelled));
        _reporter.RecordCanceledAsync(chainId, cancels.Count);

        _logger.LogDebug("[Search] {chain} found a total of {count} ramp canceled.", chainId, cancels.Count);

        await Task.WhenAll(requestCancelsTasks);
    }

    // search ramp manually executed event 
    private async Task ExecuteRampManuallyExecutedAsync(string chainId, long to, long from)
    {
        var manuallyExecutes = await _provider.SearchRampManuallyExecutedAsync(chainId, to, from);
        var requestManuallyExecutedTasks = manuallyExecutes.Select(requestCancelled =>
            _provider.HandleRampRequestManuallyExecutedLogEventAsync(requestCancelled));
        _reporter.RecordManuallyExecutedAsync(chainId, manuallyExecutes.Count);

        _logger.LogDebug("[Search] {chain} found a total of {count} ramp manually executed.", chainId,
            manuallyExecutes.Count);

        await Task.WhenAll(requestManuallyExecutedTasks);
    }
}