using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using AElf.CSharp.Core;
using AetherLink.AIServer.Core.Dtos;
using AetherLink.AIServer.Core.Enclave;
using AetherLink.AIServer.Core.Helper;
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
    private long _searchedHeight;
    private readonly ILogger<Searcher> _logger;
    private readonly ITransmitter _transmitter;
    private readonly SearcherOption _searcherOption;
    private readonly IEnclaveManager _enclaveManager;
    private readonly IStorageProvider _storageProvider;
    private readonly IIndexerProvider _indexerProvider;

    public Searcher(AbpAsyncTimer timer, IOptions<SearcherOption> workerOptions, ILogger<Searcher> logger,
        IServiceScopeFactory serviceScopeFactory, IIndexerProvider indexerProvider, IEnclaveManager enclaveManager
        , ITransmitter transmitter, IStorageProvider storageProvider) : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _transmitter = transmitter;
        _storageProvider = storageProvider;
        _enclaveManager = enclaveManager;
        _indexerProvider = indexerProvider;
        _searcherOption = workerOptions.Value;
        Timer.Period = _searcherOption.Timer;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        // _logger.LogDebug($"test {_searcherOption.ChainId}");

        var chainId = _searcherOption.ChainId;
        if (_searchedHeight == 0) await SearchWorkerInitializing(chainId);

        var indexLatestBlockHeight = await _indexerProvider.GetIndexBlockHeightAsync(chainId);
        var startHeight = _searchedHeight;

        if (indexLatestBlockHeight <= startHeight)
        {
            _logger.LogDebug(
                "[Search] {chain} startHeight is {searched} confirmed height is {latest}, height hasn't been updated yet, will try later.",
                chainId, startHeight, indexLatestBlockHeight);
            return;
        }

        startHeight = startHeight.Add(1);
        _logger.LogDebug("[Search] {chainId} startHeight: {s}, targetHeight:{t}.", chainId, startHeight,
            indexLatestBlockHeight);

        var requests = await _indexerProvider.SubscribeAIRequestsAsync(chainId, startHeight, indexLatestBlockHeight);
        await Task.WhenAll(requests.Select(ProcessRequestAsync));
        var transmit =
            await _indexerProvider.SubscribeAIReportTransmittedsAsync(chainId, startHeight, indexLatestBlockHeight);
        await Task.WhenAll(transmit.Select(ProcessTransmittedAsync));

        _searchedHeight = indexLatestBlockHeight;
        await _storageProvider.SetAsync(GenerateSearchHeightStorageKey(chainId), new SearchHeightDto
        {
            BlockHeight = indexLatestBlockHeight
        });
    }

    private async Task ProcessRequestAsync(AIRequestDto request)
    {
        var data = await _enclaveManager.CreateAsync(request);
        var ctx = OracleContextHelper.GenerateOracleContextAsync(request.ChainId, request.RequestId);
        var (report, signedData) = await _enclaveManager.ProcessAsync(ctx, data);
        await _transmitter.SendTransmitTransactionAsync(ctx, report, signedData);
    }

    private async Task ProcessTransmittedAsync(AIReportTransmittedDto transmitted)
    {
        await _enclaveManager.FinishAsync(transmitted);
    }

    private async Task SearchWorkerInitializing(string chainId)
    {
        _logger.LogInformation("[Searcher] Aetherlink ai server initializing in {chain}.", chainId);

        var searchHeightData =
            await _storageProvider.GetAsync<SearchHeightDto>(GenerateSearchHeightStorageKey(chainId));
        _searchedHeight = searchHeightData?.BlockHeight ?? (_searcherOption.StartHeight == -1
            ? await _indexerProvider.GetIndexBlockHeightAsync(chainId)
            : _searcherOption.StartHeight);
    }

    private string GenerateSearchHeightStorageKey(string chainId) => $"search-{chainId}";
}