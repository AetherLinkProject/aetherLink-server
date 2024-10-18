using System;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Common.TonIndexer;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Scheduler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherLink.Worker.Core.Worker;

public class TonIndexerWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly TonHelper _tonHelper;
    private readonly ILogger<TonIndexerWorker> _logger;
    private readonly TonIndexerRouter _tonIndexerRouter;
    private readonly IRampMessageProvider _rampMessageProvider;
    private readonly IRampRequestSchedulerJob _requestScheduler;
    private readonly ITonStorageProvider _tonStorageProvider;
    private readonly ISchedulerService _scheduler;

    public TonIndexerWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        TonIndexerRouter tonIndexerRouter, TonHelper tonHelper,
        ILogger<TonIndexerWorker> logger,
        IRampMessageProvider rampMessageProvider,
        ITonStorageProvider tonStorageProvider,
        IRampRequestSchedulerJob rampRequestSchedulerJob,
        ISchedulerService scheduler) : base(timer,
        serviceScopeFactory)
    {
        _tonHelper = tonHelper;
        _logger = logger;
        _tonIndexerRouter = tonIndexerRouter;
        _rampMessageProvider = rampMessageProvider;
        _tonStorageProvider = tonStorageProvider;
        _requestScheduler = rampRequestSchedulerJob;
        _scheduler = scheduler;

        timer.Period = 1000 * TonEnvConstants.PullTransactionMinWaitSecond;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var indexerInfo = await _tonStorageProvider.GetTonIndexerInfoAsync();

        var (transactionList, currentIndexerInfo) = await _tonIndexerRouter.GetSubsequentTransaction(indexerInfo);
        var dtNow = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
        if (transactionList == null || transactionList.Count == 0)
        {
            if (currentIndexerInfo == null) return;
            currentIndexerInfo.IndexerTime = dtNow;
            await _tonStorageProvider.SetTonIndexerInfoAsync(currentIndexerInfo);
            return;
        }

        for (var i = 0; i < transactionList.Count; i++)
        {
            var tx = transactionList[i];
            if (!tx.Aborted && !tx.Bounced && tx.ExitCode == 0)
            {
                switch (tx.OpCode)
                {
                    case TonOpCodeConstants.ForwardTx:
                        await TonForwardTxHandle(tx);
                        break;
                    case TonOpCodeConstants.ReceiveTx:
                        // todo: receive logic
                        break;
                    case TonOpCodeConstants.ResendTx:
                        await HandleTonResendTx(tx);
                        break;
                    default:
                        continue;
                }
            }
            else
            {
                _logger.LogInformation(
                    $"[Ton indexer] transaction execute error, detail message is:{JsonConvert.SerializeObject(tx)}");
            }

            // update indexer info
            indexerInfo.IndexerTime = dtNow;
            indexerInfo.SkipCount = 0;
            indexerInfo.LatestTransactionHash = tx.Hash;
            indexerInfo.LatestTransactionLt = tx.TransactionLt;
            indexerInfo.BlockHeight = tx.SeqNo;
            if (i < transactionList.Count - 1)
            {
                await _tonStorageProvider.SetTonIndexerInfoAsync(indexerInfo);
            }
            else
            {
                await _tonStorageProvider.SetTonIndexerInfoAsync(currentIndexerInfo);
            }
        }
    }

    private async Task TonForwardTxHandle(CrossChainToTonTransactionDto tx)
    {
        var forwardMessage = _tonHelper.AnalysisForwardTransaction(tx);
        if (forwardMessage == null)
        {
            _logger.LogWarning(
                $"[Ton indexer] AnalysisForwardTransaction Get Null, CrossChainToTonTransactionDto is:{JsonConvert.SerializeObject(tx)}");
            return;
        }

        var rampMessageData = await _rampMessageProvider.GetAsync(forwardMessage.MessageId);
        if (rampMessageData == null)
        {
            _logger.LogWarning(
                $"[Ton indexer] forward task, messageId:{forwardMessage.MessageId} not find in system, transaction hash:{tx.Hash}");
            return;
        }

        if (rampMessageData.ResendTransactionBlockHeight > tx.SeqNo)
        {
            _logger.LogWarning(
                $"[Ton indexer] forward message block height conflict, block:{rampMessageData.ResendTransactionBlockHeight}-{tx.SeqNo}, tx:{rampMessageData.ResendTransactionId}-{tx.Hash}");
            return;
        }

        if (rampMessageData.State != RampRequestState.Committed)
        {
            _logger.LogWarning(
                $"[Ton indexer] MessageId:{forwardMessage.MessageId} state error,current status is:{rampMessageData.State}, but receive a chain transaction");
        }

        // update message status
        rampMessageData.State = RampRequestState.Confirmed;
        rampMessageData.ResendTransactionBlockHeight = tx.SeqNo;
        rampMessageData.ResendTransactionId = tx.Hash;
        await _rampMessageProvider.SetAsync(rampMessageData);

        // cancel check transaction status task
        _scheduler.CancelScheduler(rampMessageData);
    }

    private async Task HandleTonResendTx(CrossChainToTonTransactionDto tx)
    {
        var resendMessage = _tonHelper.AnalysisResendTransaction(tx);
        if (resendMessage == null)
        {
            _logger.LogInformation(
                $"[Ton indexer] AnalysisResendTransaction Get Null, CrossChainToTonTransactionDto is:{JsonConvert.SerializeObject(tx)}");
            return;
        }

        var rampMessageData = await _rampMessageProvider.GetAsync(resendMessage.MessageId);
        if (rampMessageData == null)
        {
            _logger.LogWarning(
                $"[Ton indexer] resend task, messageId:{resendMessage.MessageId} not find in system, block time:{tx.BlockTime} resend time:{resendMessage.ResendTime}");
            return;
        }

        if (rampMessageData.ResendTransactionBlockHeight > tx.SeqNo)
        {
            _logger.LogWarning(
                $"[Ton Indexer] receive resend transaction, but block height conflict, messageId:{resendMessage.MessageId}, current block height:{rampMessageData.ResendTransactionBlockHeight}-{tx.SeqNo}, hash compare:{rampMessageData.ResendTransactionId}-{tx.Hash}");
            return;
        }

        rampMessageData.ResendTransactionBlockTime =
            DateTimeOffset.FromUnixTimeSeconds(tx.BlockTime).DateTime;
        rampMessageData.NextCommitDelayTime = (int)resendMessage.ResendTime;
        rampMessageData.ResendTransactionId = tx.Hash;
        rampMessageData.ResendTransactionBlockHeight = tx.SeqNo;
        await _requestScheduler.Resend(rampMessageData);

        _logger.LogInformation(
            $"[Ton indexer] received resend transaction messageId:{resendMessage.MessageId}, hash:{resendMessage.Hash}, block time:{tx.BlockTime}, resend time:{resendMessage.ResendTime}");
    }
}