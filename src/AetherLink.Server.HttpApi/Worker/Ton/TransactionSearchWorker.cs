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
    private readonly IJobsReporter _jobsReporter;
    private readonly IClusterClient _clusterClient;
    private readonly ICrossChainReporter _crossChainReporter;
    private readonly ILogger<TransactionSearchWorker> _logger;

    public TransactionSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TonOptions> options, IClusterClient clusterClient, ILogger<TransactionSearchWorker> logger,
        ICrossChainReporter crossChainReporter, IJobsReporter jobsReporter) : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _jobsReporter = jobsReporter;
        _clusterClient = clusterClient;
        timer.Period = _options.TransactionSearchTimer;
        _crossChainReporter = crossChainReporter;
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
        var bodySlice = Cell.From(transaction.InMsg.MessageContent.Body).Parse();
        var opCode = bodySlice.LoadUInt(TonTransactionConstants.DefaultUIntSize);
        var messageContext = bodySlice.LoadRef().Parse();
        var messageId = Base64.ToBase64String(messageContext.LoadBytes(TonTransactionConstants.MessageIdBytesSize));

        _logger.LogDebug($"[TonSearchWorker] Get messageId: {messageId} transaction.");

        var transactionIdGrainClient = _clusterClient.GetGrain<ITransactionIdGrain>(messageId);
        var transactionIdGrainResponse = await transactionIdGrainClient.GetAsync();
        if (!transactionIdGrainResponse.Success)
        {
            _logger.LogDebug($"[TonSearchWorker] Get TransactionIdGrain {messageId} failed.");
            return;
        }

        if (transactionIdGrainResponse.Data == null)
        {
            _logger.LogWarning($"[TonSearchWorker] TransactionId grain {messageId} not exist, no need to update.");
            return;
        }

        var grainId = transactionIdGrainResponse.Data.GrainId;
        var requestGrain = _clusterClient.GetGrain<ICrossChainRequestGrain>(messageId);
        var response = await requestGrain.GetAsync();
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
            _logger.LogWarning($"[TonSearchWorker] TransactionId grain {grainId} not exist, no need to update.");
            await requestGrain.CreateAsync(crossChainRequestData);
            return;
        }

        crossChainRequestData = response.Data;
        var result = await requestGrain.UpdateAsync(crossChainRequestData);
        _logger.LogDebug($"[TonSearchWorker] Update {grainId} request {result.Success}");
    }

    private async Task CreateRequestAsync(TonTransactionGrainDto transaction)
    {
        _jobsReporter.ReportCommittedReport(TonTransactionConstants.TonChainId.ToString(),
            StartedRequestTypeName.Crosschain);
        var messageId = HashHelper.ComputeFrom(transaction.Hash).ToHex();
        var requestGrain = _clusterClient.GetGrain<ICrossChainRequestGrain>(messageId);
        if (transaction.OutMsgs == null)
        {
            _logger.LogWarning("[TonSearchWorker] Invalid out messages");
            return;
        }

        var receiveSlice = Cell.From(transaction.OutMsgs[0].MessageContent.Body).Parse();
        var _ = (long)receiveSlice.LoadInt(TonTransactionConstants.DefaultIntSize);
        var targetChainId = (long)receiveSlice.LoadInt(TonTransactionConstants.ChainIdSize);
        _crossChainReporter.ReportCrossChainRequest(messageId, TonTransactionConstants.TonChainId.ToString(),
            targetChainId.ToString());
        var startTime = transaction.Now;
        var crossChainRequestData = new CrossChainRequestGrainDto
        {
            SourceChainId = TonTransactionConstants.TonChainId,
            TargetChainId = targetChainId,
            MessageId = messageId,
            Status = CrossChainStatus.Started.ToString(),
            StartTime = startTime
        };

        var result = await requestGrain.CreateAsync(crossChainRequestData);
        _logger.LogDebug($"[TonSearchWorker] Create {messageId} request {result.Success}");

        var traceIdGrain = _clusterClient.GetGrain<ITraceIdGrain>(transaction.TraceId);
        var traceCreatedResult = await traceIdGrain.UpdateAsync(new() { GrainId = messageId });
        _logger.LogDebug(
            $"[TonSearchWorker] Create {messageId} request traceId {transaction.TraceId} {traceCreatedResult.Success}");
    }
}