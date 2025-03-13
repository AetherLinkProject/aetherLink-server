using AetherLink.Server.Grains;
using AetherLink.Server.Grains.Grain.Indexer;
using AetherLink.Server.Grains.Grain.Request;
using AetherLink.Server.HttpApi.Constants;
using AetherLink.Server.HttpApi.Dtos;
using AetherLink.Server.HttpApi.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherLink.Server.HttpApi.Worker.AELF;

public class CommitSearchWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly AELFOptions _options;
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<CommitSearchWorker> _logger;

    public CommitSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<AELFOptions> options, IClusterClient clusterClient, ILogger<CommitSearchWorker> logger) : base(
        timer, serviceScopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _clusterClient = clusterClient;
        timer.Period = _options.CommitSearchTimer;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var client = _clusterClient.GetGrain<IAeFinderGrain>(GrainKeyConstants.ConfirmBlockHeightGrainKey);
        var result = await client.GetBlockHeightAsync();
        if (!result.Success)
        {
            _logger.LogWarning("[CommitSearchWorker] Get Block Height failed");
            return;
        }

        await Task.WhenAll(result.Data.Select(d => HandleRequestsAsync(d.ChainId, d.LastIrreversibleBlockHeight)));
    }

    private async Task HandleRequestsAsync(string chainId, long confirmedHeight)
    {
        var grainId = GrainIdHelper.GenerateGrainId(GrainKeyConstants.CommitWorkerConsumedBlockHeightGrainKey, chainId);
        var client = _clusterClient.GetGrain<IAELFConsumedBlockHeightGrain>(grainId);
        var consumedHeight = await client.GetConsumedHeightAsync();
        if (!consumedHeight.Success)
        {
            _logger.LogWarning($"[CommitSearchWorker] Get {chainId} consumed block height failed");
            return;
        }

        if (consumedHeight.Data == 0)
        {
            await client.UpdateConsumedHeightAsync(confirmedHeight);
            _logger.LogInformation($"[CommitSearchWorker] Initial {chainId} consumed block height. ");
            return;
        }

        var consumedBlockHeight = consumedHeight.Data + 1;
        // if (confirmedHeight < consumedBlockHeight)
        // {
        // _logger.LogWarning(
        //     $"[CommitSearchWorker] Waiting for {chainId} block confirmed, consumedBlockHeight:{consumedBlockHeight} confirmedHeight:{confirmedHeight}.");
        //     return;
        // }

        _logger.LogDebug(
            $"[CommitSearchWorker] Get {chainId} Block Height {confirmedHeight}");

        var aeFinderGrainClient =
            _clusterClient.GetGrain<IAeFinderGrain>(GrainKeyConstants.SearchRequestsCommittedGrainKey);
        var requests =
            await aeFinderGrainClient.SearchRequestsCommittedAsync(chainId, confirmedHeight, consumedBlockHeight);
        if (!requests.Success)
        {
            _logger.LogError($"[CommitSearchWorker] {chainId} Get requests failed");
        }

        var tasks = requests.Data.Select(HandleReportCommittedAsync);
        await Task.WhenAll(tasks);
        _logger.LogInformation("[CommitSearchWorker] {chain} found a total of {count} committed report.", chainId,
            tasks.Count());

        await client.UpdateConsumedHeightAsync(confirmedHeight);
        _logger.LogDebug($"[CommitSearchWorker] {chainId} Block Height consumed at {confirmedHeight}");
    }

    private async Task HandleReportCommittedAsync(AELFRampRequestGrainDto requestData)
    {
        var requestGrain = _clusterClient.GetGrain<ICrossChainRequestGrain>(requestData.MessageId);
        var result = await requestGrain.UpdateAsync(new()
        {
            Id = requestData.MessageId,
            SourceChainId = requestData.SourceChainId,
            TargetChainId = requestData.TargetChainId,
            MessageId = requestData.MessageId,
            Status = CrossChainStatus.Committed.ToString()
        });

        _logger.LogDebug($"[CommitSearchWorker] Update {requestData.MessageId} committed {result.Success}");
    }
}