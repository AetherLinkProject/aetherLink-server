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
using AetherLink.Server.HttpApi.Reporter;
using AetherLink.Server.HttpApi.Provider;

namespace AetherLink.Server.HttpApi.Worker.Evm;

public class EvmSearchWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly EVMOptions _options;
    private readonly JobsReporter _jobsReporter;
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<EvmSearchWorker> _logger;

    public EvmSearchWorker(
        AbpAsyncTimer timer,
        JobsReporter jobsReporter,
        IClusterClient clusterClient,
        ILogger<EvmSearchWorker> logger,
        IOptionsSnapshot<EVMOptions> options,
        IServiceScopeFactory serviceScopeFactory
    ) : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _jobsReporter = jobsReporter;
        _clusterClient = clusterClient;
        timer.Period = _options.TransactionSearchTimer;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.LogDebug("[EvmSearchWorker] Start to search EVM requests...");

        var client = _clusterClient.GetGrain<IEvmGrain>(GrainKeyConstants.ConfirmBlockHeightGrainKey);
        var result = await client.GetBlockHeightAsync();
        if (!result.Success)
        {
            _logger.LogWarning("[EvmSearchWorker] Get Block Height failed");
            return;
        }

        await Task.WhenAll(result.Data.Select(d => HandleRequestsAsync(d.NetworkName, d.ConsumedBlockHeight)));
    }

    private async Task HandleRequestsAsync(string networkName, long latestBlockHeight)
    {
        _logger.LogDebug($"[EvmSearchWorker] Start handler {networkName} request ...");

        var grainId =
            GrainIdHelper.GenerateGrainId(GrainKeyConstants.RequestWorkerConsumedBlockHeightGrainKey, networkName);
        var client = _clusterClient.GetGrain<IEvmConsumedBlockHeightGrain>(grainId);
        var consumedHeight = await client.GetConsumedHeightAsync();
        if (!consumedHeight.Success)
        {
            _logger.LogWarning($"[EvmSearchWorker] Get {networkName} consumed block height failed");
            return;
        }

        var consumedBlockHeight = consumedHeight.Data;
        if (consumedBlockHeight == 0)
        {
            await client.UpdateConsumedHeightAsync(latestBlockHeight);
            _logger.LogInformation($"[EvmSearchWorker] Initial {networkName} consumed block height. ");
            return;
        }

        var confirmedBlockHeight = latestBlockHeight - _options.SubscribeBlocksDelay;
        if (consumedBlockHeight + _options.SubscribeBlocksStep >= confirmedBlockHeight)
        {
            _logger.LogDebug(
                $"[EvmSearchWorker] Current: {consumedBlockHeight}, Latest: {latestBlockHeight}, Waiting for syncing {networkName} latest block info.");
            return;
        }

        _logger.LogInformation(
            $"[EvmSearchWorker] {networkName} Starting HTTP query from block {consumedBlockHeight} to latestBlock {confirmedBlockHeight}");

        var from = consumedBlockHeight + 1;
        var evmIndexerGrainClient = _clusterClient.GetGrain<IEvmGrain>(GrainKeyConstants.SearchRampRequestsGrainKey);
        var requests = await evmIndexerGrainClient.SearchEvmRequestsAsync(networkName, confirmedBlockHeight, from);

        _logger.LogInformation($"[EvmSearchWorker] {networkName} get {requests.Data?.Count} requests.");

        await Task.WhenAll(requests.Data.Select(HandleCrossChainRequestAsync));
        await client.UpdateConsumedHeightAsync(confirmedBlockHeight);

        _logger.LogInformation($"[EvmSearchWorker] {networkName} confirmed at {confirmedBlockHeight}");
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
        _logger.LogDebug(
            $"[EvmSearchWorker] [HandleRequestStart] TransactionId={metadata.TransactionId}, MessageId={metadata.MessageId}");
        var grainId = metadata.TransactionId;
        var messageId = metadata.MessageId;
        var startTime = metadata.BlockTime;
        var requestGrain = _clusterClient.GetGrain<ICrossChainRequestGrain>(grainId);
        var result = await requestGrain.UpdateAsync(new()
        {
            Id = grainId,
            SourceChainId = metadata.SourceChainId,
            TargetChainId = metadata.TargetChainId,
            MessageId = messageId,
            Status = CrossChainStatus.Started.ToString(),
            StartTime = startTime
        });
        _logger.LogDebug($"[EvmSearchWorker] [HandleRequestStart] requestGrain.UpdateAsync: {result.Success}");

        var messageGrain = _clusterClient.GetGrain<ICrossChainRequestGrain>(messageId);
        var messageResult = await messageGrain.UpdateAsync(new()
        {
            Id = grainId,
            SourceChainId = metadata.SourceChainId,
            TargetChainId = metadata.TargetChainId,
            MessageId = messageId,
            Status = CrossChainStatus.Started.ToString(),
            StartTime = startTime
        });
        _logger.LogDebug($"[EvmSearchWorker] [HandleRequestStart] messageGrain.UpdateAsync: {messageResult.Success}");

        var sourceChainName = ChainIdNameHelper.ToChainName(metadata.SourceChainId);
        var targetChainName = ChainIdNameHelper.ToChainName(metadata.TargetChainId);
        _jobsReporter.ReportStartedRequest(messageId, sourceChainName, targetChainName,
            StartedRequestTypeName.Crosschain);
    }

    private async Task HandleCommittedAsync(EvmRampRequestGrainDto metadata)
    {
        _logger.LogDebug(
            $"[EvmSearchWorker] [HandleCommitted] MessageId={metadata.MessageId}, TransactionId={metadata.TransactionId}");
        var messageId = metadata.MessageId;
        var transactionIdGrainClient = _clusterClient.GetGrain<ITransactionIdGrain>(messageId);
        var transactionIdGrainResponse = await transactionIdGrainClient.GetAsync();
        if (!transactionIdGrainResponse.Success)
        {
            _logger.LogWarning($"[EvmSearchWorker] [HandleCommitted] Get TransactionIdGrain {messageId} failed.");
            return;
        }

        if (transactionIdGrainResponse.Data == null)
        {
            _logger.LogWarning(
                $"[EvmSearchWorker] [HandleCommitted] TransactionId grain {messageId} not exist, no need to update.");
            return;
        }

        var crossChainGrainId = transactionIdGrainResponse.Data.GrainId;
        var requestGrain = _clusterClient.GetGrain<ICrossChainRequestGrain>(crossChainGrainId);
        var response = await requestGrain.GetAsync();
        if (!response.Success)
        {
            _logger.LogWarning(
                $"[EvmSearchWorker] [HandleCommitted] Get crossChainRequestGrain {crossChainGrainId} failed.");
            return;
        }

        if (response.Data == null)
        {
            _logger.LogWarning(
                $"[EvmSearchWorker] [HandleCommitted] TransactionId grain {crossChainGrainId} not exist, no need to update. Creating new grain.");
            await requestGrain.CreateAsync(new()
            {
                MessageId = messageId,
                Status = CrossChainStatus.Committed.ToString()
            });
            return;
        }

        if (response.Data.Status == CrossChainStatus.Committed.ToString())
        {
            _logger.LogInformation(
                $"[EvmSearchWorker] [HandleCommitted] MessageId {messageId} already committed, skip duration report.");
            return;
        }

        var sourceChainName = ChainIdNameHelper.ToChainName(metadata.SourceChainId);
        var targetChainName = ChainIdNameHelper.ToChainName(metadata.TargetChainId);
        _jobsReporter.ReportCommittedReport(messageId, sourceChainName, targetChainName,
            StartedRequestTypeName.Crosschain);

        var duration = (metadata.BlockTime - response.Data.StartTime) / 1000.0;
        _logger.LogInformation(
            $"[EvmSearchWorker] [HandleCommitted] ReportExecutionDuration: MessageId={messageId}, ChainId={metadata.SourceChainId}, Duration={duration}s");
        _jobsReporter.ReportExecutionDuration(messageId, sourceChainName, targetChainName,
            StartedRequestTypeName.Crosschain, duration);

        response.Data.CommitTime = metadata.BlockTime;
        var result = await requestGrain.UpdateAsync(response.Data);
        _logger.LogDebug($"[EvmSearchWorker] [HandleCommitted] UpdateAsync: {result.Success}");
    }
}