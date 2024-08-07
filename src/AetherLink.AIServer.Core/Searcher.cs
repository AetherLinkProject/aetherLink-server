using System.Linq;
using System.Threading.Tasks;
using AetherLink.AIServer.Core.Enclave;
using AetherLink.AIServer.Core.Options;
using AetherLink.AIServer.Core.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherLink.AIServer.Core;

public class Searcher : AsyncPeriodicBackgroundWorkerBase
{
    private readonly ILogger<Searcher> _logger;
    private readonly SearcherOption _searcherOption;
    private readonly IEnclaveManager _enclaveManager;
    private readonly IIndexerProvider _indexerProvider;

    public Searcher(AbpAsyncTimer timer, IOptions<SearcherOption> workerOptions, ILogger<Searcher> logger,
        IServiceScopeFactory serviceScopeFactory, IIndexerProvider indexerProvider, IEnclaveManager enclaveManager) :
        base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _enclaveManager = enclaveManager;
        _indexerProvider = indexerProvider;
        _searcherOption = workerOptions.Value;
        Timer.Period = _searcherOption.Timer;
        _searcherOption = workerOptions.Value;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.LogDebug($"test {_searcherOption.ChainId}");

        var requests = await _indexerProvider.SubscribeAIRequestsAsync("AELF", 8852400, 8852500);

        await Task.WhenAll(requests.Select(_enclaveManager.CreateAsync));
    }
}