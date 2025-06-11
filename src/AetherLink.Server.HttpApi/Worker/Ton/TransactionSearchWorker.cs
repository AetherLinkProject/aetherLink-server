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
using TonSdk.Core.Boc;
using AetherLink.Server.HttpApi.Reporter;
using Org.BouncyCastle.Utilities.Encoders;

namespace AetherLink.Server.HttpApi.Worker.Ton;

public class TransactionSearchWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly TonOptions _options;
    private readonly JobsReporter _jobsReporter;
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<TransactionSearchWorker> _logger;

    public TransactionSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TonOptions> options, IClusterClient clusterClient, ILogger<TransactionSearchWorker> logger,
        JobsReporter jobsReporter) : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _jobsReporter = jobsReporter;
        _clusterClient = clusterClient;
        timer.Period = _options.TransactionSearchTimer;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.LogInformation("[TonSearchWorker] Start searching TON transactions...");
        var client = _clusterClient.GetGrain<ITonIndexerGrain>(GrainKeyConstants.SearchTransactionGrainKey);
        var result = await client.SearchTonTransactionsAsync();

        if (!result.Success)
        {
            _logger.LogWarning("[TonSearchWorker] Search TON transactions failed...");
            return;
        }

        _logger.LogInformation($"[TonSearchWorker] Get {result.Data.Count} TON transactions...");
        await Task.WhenAll(result.Data.Select(HandlerTonTransactionAsync));
    }

    private async Task HandlerTonTransactionAsync(TonTransactionGrainDto transaction)
    {
        _logger.LogDebug(
            $"[TonSearchWorker] Get TON transaction traceId: {transaction.TraceId}, OpCode: {transaction.InMsg.Opcode}");

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
        _logger.LogDebug($"[TonSearchWorker] [UpdateRequestStateAsync] Start. TraceId: {transaction.TraceId}");
        try
        {
            var bodySlice = Cell.From(transaction.InMsg.MessageContent.Body).Parse();
            var opCode = bodySlice.LoadUInt(TonTransactionConstants.DefaultUIntSize);
            var messageContext = bodySlice.LoadRef().Parse();
            var messageId = Base64.ToBase64String(messageContext.LoadBytes(TonTransactionConstants.MessageIdBytesSize));
            var transactionIdGrainClient = _clusterClient.GetGrain<ITransactionIdGrain>(messageId);
            _logger.LogDebug(
                $"[TonSearchWorker] [UpdateRequestStateAsync] Getting TransactionIdGrain for messageId: {messageId}");

            var transactionIdGrainResponse = await transactionIdGrainClient.GetAsync();
            _logger.LogDebug(
                $"[TonSearchWorker] [UpdateRequestStateAsync] TransactionIdGrain response: Success={transactionIdGrainResponse.Success}, DataNull={transactionIdGrainResponse.Data == null}");
            if (!transactionIdGrainResponse.Success)
            {
                _logger.LogDebug($"[TonSearchWorker] Get TransactionIdGrain {messageId} failed.");
                return;
            }

            if (transactionIdGrainResponse.Data == null)
            {
                _logger.LogWarning(
                    $"[TonSearchWorker] TransactionId grain messageId {messageId} not exist, no need to update.");
                return;
            }

            var grainId = transactionIdGrainResponse.Data.GrainId;
            var requestGrain = _clusterClient.GetGrain<ICrossChainRequestGrain>(grainId);
            var response = await requestGrain.GetAsync();
            _logger.LogDebug(
                $"[TonSearchWorker] [UpdateRequestStateAsync] Getting CrossChainRequestGrain for messageId: {messageId} response: Success={response.Success}, DataNull={response.Data == null}");
            if (!response.Success)
            {
                _logger.LogWarning($"[TonSearchWorker] Get crossChainRequestGrain {grainId} failed.");
                return;
            }

            var crossChainRequestData = new CrossChainRequestGrainDto
            {
                Status = transaction.InMsg.Opcode == TonOpCodeConstants.Forward
                    ? CrossChainStatus.Committed.ToString()
                    : CrossChainStatus.PendingResend.ToString()
            };

            if (response.Data == null)
            {
                _logger.LogWarning(
                    $"[TonSearchWorker] TransactionId grain {grainId} not exist, no need to update. Creating new request.");
                await requestGrain.CreateAsync(crossChainRequestData);
                return;
            }

            _jobsReporter.ReportCommittedReport(response.Data.MessageId, response.Data.SourceChainId.ToString(),
                TonTransactionConstants.TonChainId.ToString(), StartedRequestTypeName.Crosschain);

            var duration = (transaction.Now - response.Data.StartTime) / 1000.0;
            _logger.LogDebug(
                $"[TonSearchWorker] [UpdateRequestStateAsync] Reporting execution duration. MessageId: {response.Data.MessageId}, SourceChainId: {response.Data.SourceChainId}, TargetChainId: {TonTransactionConstants.TonChainId}, Duration: {duration}");
            _jobsReporter.ReportExecutionDuration(response.Data.MessageId, response.Data.SourceChainId.ToString(),
                TonTransactionConstants.TonChainId.ToString(), StartedRequestTypeName.Crosschain, duration);

            response.Data.CommitTime = transaction.Now;
            crossChainRequestData = response.Data;
            var result = await requestGrain.UpdateAsync(crossChainRequestData);
            _logger.LogDebug($"[TonSearchWorker] Update {grainId} request {result.Success}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"[TonSearchWorker] [UpdateRequestStateAsync] Exception occurred. TraceId: {transaction.TraceId}");
            throw;
        }
    }

    private async Task CreateRequestAsync(TonTransactionGrainDto transaction)
    {
        var messageId = transaction.Hash;
        var requestGrain = _clusterClient.GetGrain<ICrossChainRequestGrain>(messageId);
        if (transaction.OutMsgs == null)
        {
            _logger.LogWarning("[TonSearchWorker] Invalid out messages");
            return;
        }

        var receiveSlice = Cell.From(transaction.OutMsgs[0].MessageContent.Body).Parse();
        var _ = (long)receiveSlice.LoadInt(TonTransactionConstants.DefaultIntSize);
        var targetChainId = (long)receiveSlice.LoadInt(TonTransactionConstants.ChainIdSize);
        var crossChainRequestData = new CrossChainRequestGrainDto
        {
            SourceChainId = TonTransactionConstants.TonChainId,
            TargetChainId = targetChainId,
            MessageId = messageId,
            Status = CrossChainStatus.Started.ToString(),
            StartTime = transaction.Now
        };

        await requestGrain.CreateAsync(crossChainRequestData);
        var traceIdGrain = _clusterClient.GetGrain<ITraceIdGrain>(transaction.TraceId);
        var traceCreatedResult = await traceIdGrain.UpdateAsync(new() { GrainId = messageId });
        _logger.LogDebug(
            $"[TonSearchWorker] Create {messageId} request traceId {transaction.TraceId} {traceCreatedResult.Success}");

        _jobsReporter.ReportStartedRequest(messageId, TonTransactionConstants.TonChainId.ToString(),
            targetChainId.ToString(), StartedRequestTypeName.Crosschain);
    }
}