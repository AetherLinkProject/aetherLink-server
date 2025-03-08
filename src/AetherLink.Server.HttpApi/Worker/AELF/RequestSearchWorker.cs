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

public class RequestSearchWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly AELFOptions _options;
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<RequestSearchWorker> _logger;

    public RequestSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<AELFOptions> options, IClusterClient clusterClient,
        ILogger<RequestSearchWorker> logger) : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _clusterClient = clusterClient;
        timer.Period = _options.RequestSearchTimer;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var client = _clusterClient.GetGrain<IAeFinderGrain>(GrainKeyConstants.ConfirmBlockHeightGrainKey);
        var result = await client.GetBlockHeightAsync();
        if (!result.Success)
        {
            _logger.LogWarning("[RequestSearchWorker]Get Block Height failed");
            return;
        }

        await Task.WhenAll(result.Data.Select(d => HandleRequestsAsync(d.ChainId, d.LastIrreversibleBlockHeight)));
    }

    private async Task HandleRequestsAsync(string chainId, long confirmedHeight)
    {
        var grainId =
            GrainIdHelper.GenerateGrainId(GrainKeyConstants.RequestWorkerConsumedBlockHeightGrainKey, chainId);
        var client = _clusterClient.GetGrain<IAELFConsumedBlockHeightGrain>(grainId);
        var consumedHeight = await client.GetConsumedHeightAsync();
        if (!consumedHeight.Success)
        {
            _logger.LogWarning($"[RequestSearchWorker] Get {chainId} consumed block height failed");
            return;
        }

        if (consumedHeight.Data == 0)
        {
            await client.UpdateConsumedHeightAsync(confirmedHeight);
            _logger.LogInformation($"[RequestSearchWorker] Initial {chainId} consumed block height. ");
            return;
        }

        var consumedBlockHeight = consumedHeight.Data + 1;
        if (confirmedHeight < consumedBlockHeight)
        {
            _logger.LogWarning(
                $"[RequestSearchWorker] Waiting for {chainId} block confirmed, consumedBlockHeight:{consumedBlockHeight} confirmedHeight:{confirmedHeight}.");
            return;
        }

        _logger.LogDebug(
            $"[RequestSearchWorker] Get {chainId} Block Height {confirmedHeight}");

        var aeFinderGrainClient = _clusterClient.GetGrain<IAeFinderGrain>(GrainKeyConstants.SearchRampRequestsGrainKey);
        var requests =
            await aeFinderGrainClient.SearchRampRequestsAsync(chainId, confirmedHeight, consumedBlockHeight);
        if (!requests.Success)
        {
            _logger.LogError($"[RequestSearchWorker] {chainId} Get requests failed");
        }

        var tasks = requests.Data.Select(HandleRampRequestAsync);
        await Task.WhenAll(tasks);
        _logger.LogDebug("[RequestSearchWorker] {chain} found a total of {count} ramp requests.", chainId,
            tasks.Count());

        await client.UpdateConsumedHeightAsync(confirmedHeight);
        _logger.LogDebug($"[RequestSearchWorker] {chainId} Block Height consumed at {confirmedHeight}");
    }

    private async Task HandleRampRequestAsync(AELFRampRequestGrainDto requestData)
    {
        _logger.LogDebug($"[RequestSearchWorker] Start to create cross chain request for {requestData.MessageId}");
        var requestGrain = _clusterClient.GetGrain<ICrossChainRequestGrain>(requestData.TransactionId);
        var result = await requestGrain.UpdateAsync(new()
        {
            Id = requestData.MessageId,
            SourceChainId = requestData.SourceChainId,
            TargetChainId = requestData.TargetChainId,
            MessageId = requestData.MessageId,
            Status = CrossChainStatus.Started.ToString()
        });

        _logger.LogDebug($"[RequestSearchWorker] Update {requestData.TransactionId} started {result.Success}");

        var transactionIdGrainClient = _clusterClient.GetGrain<ITransactionIdGrain>(requestData.MessageId);
        var transactionIdUpdateResult =
            await transactionIdGrainClient.UpdateAsync(new() { GrainId = requestData.TransactionId });
        _logger.LogDebug(
            $"[RequestSearchWorker] Update {requestData.TransactionId} messageId {requestData.MessageId} started {transactionIdUpdateResult.Success}");
    }
}