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

namespace AetherLink.Server.HttpApi.Worker.Evm;

public class EvmSearchWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly EVMOptions _options;
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<EvmSearchWorker> _logger;

    public EvmSearchWorker(
        AbpAsyncTimer timer,
        IClusterClient clusterClient,
        ILogger<EvmSearchWorker> logger,
        IOptionsSnapshot<EVMOptions> options,
        IServiceScopeFactory serviceScopeFactory
    ) : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _clusterClient = clusterClient;
        timer.Period = _options.TransactionSearchTimer;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var client = _clusterClient.GetGrain<IEvmGrain>(GrainKeyConstants.ConfirmBlockHeightGrainKey);
        var result = await client.GetBlockHeightAsync();
        if (!result.Success)
        {
            _logger.LogWarning("[RequestSearchWorker]Get Block Height failed");
            return;
        }

        await Task.WhenAll(result.Data.Select(d => HandleRequestsAsync(d.NetworkName, d.ConsumedBlockHeight)));
    }

    private async Task HandleRequestsAsync(string networkName, long confirmedHeight)
    {
        var grainId =
            GrainIdHelper.GenerateGrainId(GrainKeyConstants.RequestWorkerConsumedBlockHeightGrainKey, networkName);
        var client = _clusterClient.GetGrain<IEvmConsumedBlockHeightGrain>(grainId);
        var consumedHeight = await client.GetConsumedHeightAsync();
        if (!consumedHeight.Success)
        {
            _logger.LogWarning($"[RequestSearchWorker] Get {networkName} consumed block height failed");
            return;
        }

        var consumedBlockHeight = consumedHeight.Data;
        if (consumedBlockHeight == 0)
        {
            await client.UpdateConsumedHeightAsync(confirmedHeight);
            _logger.LogInformation($"[RequestSearchWorker] Initial {networkName} consumed block height. ");
            return;
        }

        var safeBlockHeight = confirmedHeight - _options.SubscribeBlocksDelay;
        if (consumedBlockHeight >= safeBlockHeight)
        {
            _logger.LogDebug(
                $"[EvmSearchWorker] Current: {consumedBlockHeight}, Latest: {confirmedHeight}, Waiting for syncing {networkName} latest block info.");
            return;
        }

        _logger.LogInformation(
            $"[EvmSearchWorker] {networkName} Starting HTTP query from block {consumedBlockHeight} to latestBlock {confirmedHeight}");

        var from = consumedBlockHeight + 1;
        var evmIndexerGrainClient = _clusterClient.GetGrain<IEvmGrain>(GrainKeyConstants.SearchRampRequestsGrainKey);
        var requests = await evmIndexerGrainClient.SearchEvmRequestsAsync(networkName, safeBlockHeight, from);
        await Task.WhenAll(requests.Data.Select(HandleCrossChainRequestAsync));

        await client.UpdateConsumedHeightAsync(confirmedHeight);
    }

    private async Task HandleCrossChainRequestAsync(EvmRampRequestGrainDto metadata)
    {
        switch (metadata.Type)
        {
            case CrossChainTransactionType.CrossChainSend:
                await HandleRequestStartAsync(metadata);
                break;
            case CrossChainTransactionType.CrossChainReceive:
                await HandleCommittedAsync(metadata);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private async Task HandleRequestStartAsync(EvmRampRequestGrainDto metadata)
    {
        var grainId = metadata.TransactionId;
        var messageId = metadata.MessageId;
        var requestGrain = _clusterClient.GetGrain<ICrossChainRequestGrain>(grainId);
        var result = await requestGrain.UpdateAsync(new()
        {
            Id = grainId,
            SourceChainId = metadata.SourceChainId,
            TargetChainId = metadata.TargetChainId,
            MessageId = messageId,
            Status = CrossChainStatus.Started.ToString()
        });
        _logger.LogDebug($"[EvmSearchServer] Create {grainId} {messageId} started {result.Success}");

        var messageGrain = _clusterClient.GetGrain<ICrossChainRequestGrain>(messageId);
        var messageResult = await messageGrain.UpdateAsync(new()
        {
            Id = grainId,
            SourceChainId = metadata.SourceChainId,
            TargetChainId = metadata.TargetChainId,
            MessageId = messageId,
            Status = CrossChainStatus.Started.ToString()
        });

        _logger.LogDebug($"[EvmSearchServer] Create {grainId} message grain {messageResult.Success}");
    }

    private async Task HandleCommittedAsync(EvmRampRequestGrainDto metadata)
    {
        var messageId = metadata.TransactionId;

        // find request by message id
        var transactionIdGrainClient = _clusterClient.GetGrain<ITransactionIdGrain>(messageId);
        var transactionIdGrainResponse = await transactionIdGrainClient.GetAsync();
        if (!transactionIdGrainResponse.Success)
        {
            _logger.LogDebug($"[EvmSearchServer] Get TransactionIdGrain {messageId} failed.");
            return;
        }

        if (transactionIdGrainResponse.Data == null)
        {
            _logger.LogWarning($"[EvmSearchServer] TransactionId grain {messageId} not exist, no need to update.");
            return;
        }

        var crossChainGrainId = transactionIdGrainResponse.Data.GrainId;
        var requestGrain = _clusterClient.GetGrain<ICrossChainRequestGrain>(crossChainGrainId);
        var response = await requestGrain.GetAsync();
        if (!response.Success)
        {
            _logger.LogWarning($"[EvmSearchServer] Get crossChainRequestGrain {crossChainGrainId} failed.");
            return;
        }

        if (response.Data == null)
        {
            _logger.LogWarning(
                $"[EvmSearchServer] TransactionId grain {crossChainGrainId} not exist, no need to update.");
            await requestGrain.CreateAsync(new()
            {
                MessageId = messageId,
                Status = CrossChainStatus.Committed.ToString(),
            });
            return;
        }

        var result = await requestGrain.UpdateAsync(response.Data);

        _logger.LogDebug(
            $"[EvmSearchServer] Update {metadata.TransactionId} {messageId} committed {result.Success}");
    }
}