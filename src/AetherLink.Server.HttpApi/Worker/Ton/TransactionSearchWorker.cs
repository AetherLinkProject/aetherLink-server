using AElf;
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
using Org.BouncyCastle.Utilities.Encoders;
using TonSdk.Core.Boc;

namespace AetherLink.Server.HttpApi.Worker.Ton;

public class TransactionSearchWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly TonOptions _options;
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<TransactionSearchWorker> _logger;

    public TransactionSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TonOptions> options, IClusterClient clusterClient, ILogger<TransactionSearchWorker> logger) :
        base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _clusterClient = clusterClient;
        timer.Period = _options.TransactionSearchTimer;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var client = _clusterClient.GetGrain<ITonIndexerGrain>(GrainKeyConstants.SearchTransactionGrainKey);
        var result = await client.SearchTonTransactionsAsync();

        if (!result.Success) return;
        await Task.WhenAll(result.Data.Select(HandlerTonTransactionAsync));
    }

    private async Task HandlerTonTransactionAsync(TonTransactionGrainDto transaction)
    {
        _logger.LogDebug($"[TonSearchWorker] Get TON transaction traceId: {transaction.TraceId}");

        switch (transaction.InMsg.Opcode)
        {
            case TonOpCodeConstants.Resend:
            case TonOpCodeConstants.Forward:
                await UpdateRequestStateAsync(transaction);
                break;
            case TonOpCodeConstants.Receive:
                await CreateRequestAsync(transaction);
                break;
            default:
                _logger.LogDebug(
                    $"[TonSearchWorker] Get unknown opcode {transaction.InMsg.Opcode} in {transaction.TraceId}");
                break;
        }
    }

    private async Task UpdateRequestStateAsync(TonTransactionGrainDto transaction)
    {
        var bodySlice = Cell.From(transaction.InMsg.MessageContent.Body).Parse();
        var _ = bodySlice.LoadUInt(32);
        var messageId = Base64.ToBase64String(bodySlice.LoadBytes(32));
        var transactionIdGrainClient = _clusterClient.GetGrain<ITransactionIdGrain>(messageId);
        var transactionIdGrainResponse = await transactionIdGrainClient.GetAsync();
        if (!transactionIdGrainResponse.Success)
        {
            _logger.LogDebug($"MessageId {messageId} not exist, no need to update.");
            return;
        }

        var grainId = transactionIdGrainResponse.Data.GrainId;
        var requestGrain = _clusterClient.GetGrain<ICrossChainRequestGrain>(messageId);
        var response = await requestGrain.GetAsync();
        if (!response.Success)
        {
            _logger.LogWarning($"TransactionId grain {grainId} not exist, no need to update.");
            return;
        }

        var crossChainRequestData = response.Data;
        crossChainRequestData.Status = transaction.InMsg.Opcode == TonOpCodeConstants.Forward
            ? CrossChainStatus.Committed.ToString()
            : CrossChainStatus.PendingResend.ToString();

        var result = await requestGrain.UpdateAsync(crossChainRequestData);
        _logger.LogDebug($"[TonSearchWorker] Update {grainId} request {result.Success}");
    }

    private async Task CreateRequestAsync(TonTransactionGrainDto transaction)
    {
        var messageId = HashHelper.ComputeFrom(transaction.Hash).ToHex();
        var requestGrain = _clusterClient.GetGrain<ICrossChainRequestGrain>(messageId);
        if (transaction.OutMsgs == null)
        {
            _logger.LogWarning("[TonSearchWorker] Invalid out messages");
            return;
        }

        var receiveSlice = Cell.From(transaction.OutMsgs[0].MessageContent.Body).Parse();
        var _ = (long)receiveSlice.LoadInt(64);
        var targetChainId = (long)receiveSlice.LoadInt(64);
        var crossChainRequestData = new CrossChainRequestGrainDto
        {
            SourceChainId = 1100,
            TargetChainId = targetChainId,
            MessageId = transaction.Hash,
            Status = CrossChainStatus.Started.ToString()
        };

        var result = await requestGrain.CreateAsync(crossChainRequestData);
        _logger.LogDebug($"[TonSearchWorker] Create {transaction.Hash} request {result.Success}");

        var traceIdGrain = _clusterClient.GetGrain<ITraceIdGrain>(transaction.TraceId);
        var traceCreatedResult = await traceIdGrain.UpdateAsync(new() { GrainId = transaction.Hash });
        _logger.LogDebug(
            $"[TonSearchWorker] Create {transaction.Hash} request traceId {transaction.TraceId} {traceCreatedResult.Success}");
    }
}