using System.Threading.Tasks;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider.SearcherProvider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherLink.Worker.Core.Worker;

public class TonIndexerWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly WorkerOptions _options;
    private readonly ILogger<TonIndexerWorker> _logger;
    private readonly ITonSearchWorkerProvider _searchWorkerProvider;

    public TonIndexerWorker(AbpAsyncTimer timer, IOptionsSnapshot<WorkerOptions> options,
        ITonSearchWorkerProvider searchWorkerProvider, IServiceScopeFactory serviceScopeFactory,
        ILogger<TonIndexerWorker> logger) : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        timer.Period = options.Value.TonSearchTimer;
        _searchWorkerProvider = searchWorkerProvider;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.LogDebug("[TonIndexerWorker] Start process ton transaction...");
        await _searchWorkerProvider.ExecuteSearchAsync();
    }
}