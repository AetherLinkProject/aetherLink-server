using System;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Common.TonIndexer;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
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

        InitNotFinishTask().GetAwaiter().GetResult();
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        await Task.WhenAll(CheckIndexerProviderConnect(), TonIndexer());
    }

    private async Task CheckIndexerProviderConnect()
    {
        var apiProviderList = _tonIndexerRouter.GetIndexerApiProviderList();
        foreach (var provider in apiProviderList)
        {
            var needCheckAvailable = await provider.NeedCheckConnection();
            if (needCheckAvailable)
            {
                await provider.CheckConnection();
            }
        }
    }

    private async Task TonIndexer()
    {
        var tonIndexer = await _tonStorageProvider.GetTonIndexerInfo();

        var (transactionList, currentIndexer) = await _tonIndexerRouter.GetSubsequentTransaction(tonIndexer);
        var dtNow = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
        if (transactionList is { Count: > 0 })
        {
            foreach (var tx in transactionList)
            {
                if (!tx.Aborted && !tx.Bounced && tx.ExitCode == 0)
                {
                    switch (tx.OpCode)
                    {
                        case TonOpCodeConstants.ForwardTx:
                            await TonForwardTxHandle(tx);
                            break;
                        case TonOpCodeConstants.ReceiveTx:

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
            }
        }

        if (currentIndexer != null)
        {
            currentIndexer.IndexerTime = dtNow;
            await _tonStorageProvider.SetTonIndexerInfo(currentIndexer);
        }
    }

    private async Task InitNotFinishTask()
    {
        var allTask = await _tonStorageProvider.GetAllTonTask();
        foreach (var item in allTask)
        {
            if (item.Type == TonChainTaskType.Resend)
            {
                var resendTask = item.Convert<ResendTonBaseArgs>();

                await InformResendSchedule(resendTask.MessageId, resendTask.TargetBlockGeneratorTime,
                    resendTask.ResendTime);
            }
        }
    }

    private async Task TonForwardTxHandle(CrossChainToTonTransactionDto tx)
    {
        var forwardMessage = _tonHelper.AnalysisForwardTransaction(tx);
        if (forwardMessage != null)
        {
            var rampMessageData = await _rampMessageProvider.GetAsync(forwardMessage.MessageId);
            if (rampMessageData.State != RampRequestState.Committed)
            {
                _logger.LogWarning(
                    $"[Ton indexer] MessageId:{forwardMessage.MessageId} state error,current status is:{rampMessageData.State}, but receive a chain transaction");
            }

            // update message status
            rampMessageData.State = RampRequestState.Confirmed;
            await _rampMessageProvider.SetAsync(rampMessageData);

            // delete resend task
            var tonTask = await _tonStorageProvider.GetTonTask(forwardMessage.MessageId);
            if (tonTask is { Type: TonChainTaskType.Resend })
            {
                var resendMessage = tonTask.Convert<ResendTonBaseArgs>();
                await _tonStorageProvider.DeleteTonTask(forwardMessage.MessageId);
                _logger.LogInformation(
                    $"[Ton indexer] Resend messageId:{forwardMessage.MessageId} hash:{resendMessage.TargetTxHash} has confirmed, the confirm transaction hash is:{tx.Hash}");
            }
            
            // cancel check transaction status task
            _scheduler.CancelScheduler(rampMessageData);
        }
        else
        {
            _logger.LogWarning(
                $"[Ton indexer] AnalysisForwardTransaction Get Null, CrossChainToTonTransactionDto is:{JsonConvert.SerializeObject(tx)}");
        }
    }

    private async Task HandleTonResendTx(CrossChainToTonTransactionDto tx)
    {
        var resendMessage = _tonHelper.AnalysisResendTransaction(tx);
        if (resendMessage != null)
        {
            var tonTask = await _tonStorageProvider.GetTonTask(resendMessage.MessageId);
            if (tonTask is { Type: TonChainTaskType.Resend })
            {
                var resendTask = tonTask.Convert<ResendTonBaseArgs>();
                _logger.LogWarning(
                    $"[Ton indexer] exist resend task, messageId:{resendMessage.MessageId}, block hash compare:{tx.Hash}--{resendTask.TargetTxHash}, block time compare:{resendTask.TargetBlockGeneratorTime}-{tx.BlockTime} resend time compare:{resendTask.ResendTime}-{resendMessage.ResendTime}");
            }

            var newTonTask = new ResendTonBaseArgs();
            newTonTask.MessageId = resendMessage.MessageId;
            newTonTask.TargetBlockHeight = tx.SeqNo;
            newTonTask.TargetTxHash = tx.Hash;
            newTonTask.TargetBlockGeneratorTime = tx.BlockTime;
            newTonTask.ResendTime = resendMessage.ResendTime;
            newTonTask.Status = ResendStatus.WaitConsensus;
            await _tonStorageProvider.SetTonTask(newTonTask.MessageId, new TonChainTaskDto(newTonTask));
            _logger.LogInformation(
                $"[Ton indexer] received resend transaction messageId:{resendMessage.MessageId}, hash:{resendMessage.Hash}, block time:{tx.BlockTime}, resend time:{newTonTask.ResendTime}");

            await InformResendSchedule(resendMessage.MessageId, tx.BlockTime, resendMessage.ResendTime);
        }
        else
        {
            _logger.LogInformation(
                $"[Ton indexer] AnalysisResendTransaction Get Null, CrossChainToTonTransactionDto is:{JsonConvert.SerializeObject(tx)}");
        }
    }

    private async Task InformResendSchedule(string messageId, long blockTime, long resendTime)
    {
        var rampMessageData = await _rampMessageProvider.GetAsync(messageId);
        if (rampMessageData == null)
        {
            _logger.LogWarning(
                $"[Ton indexer] resend task, messageId:{messageId} not find in system, block time:{blockTime} resend time:{resendTime}");
            return;
        }

        rampMessageData.ResendTransactionBlockTime =
            DateTimeOffset.FromUnixTimeSeconds(blockTime).DateTime;
        rampMessageData.NextCommitDelayTime = (int)resendTime;
        await _requestScheduler.Resend(rampMessageData);
        _scheduler.StartScheduler(rampMessageData, RampSchedulerType.ResendPendingScheduler);
    }
}