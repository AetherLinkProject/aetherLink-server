using System;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Common.TonIndexer;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Scheduler;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherLink.Worker.Core.Worker;

public class TonIndexerWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly TonHelper _tonHelper;
    private readonly ILogger<TonIndexerWorker> _logger;
    private readonly TonIndexerRouter _tonIndexerRouter;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IRampMessageProvider _rampMessageProvider;
    private readonly IRampRequestSchedulerJob _requestScheduler;

    public TonIndexerWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        TonIndexerRouter tonIndexerRouter, TonHelper tonHelper, IBackgroundJobManager backgroundJobManager,
        ILogger<TonIndexerWorker> logger,
        IRampMessageProvider rampMessageProvider,
        IRampRequestSchedulerJob rampRequestSchedulerJob) : base(timer,
        serviceScopeFactory)
    {
        _tonHelper = tonHelper;
        _logger = logger;
        _tonIndexerRouter = tonIndexerRouter;
        _rampMessageProvider = rampMessageProvider;
        _backgroundJobManager = backgroundJobManager;
        _requestScheduler = rampRequestSchedulerJob;

        timer.Period = 1000 * TonEnvConstants.PullTransactionMinWaitSecond;
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

    private async Task InitNotFinishTask()
    {
        var allTask = await _tonHelper.GetAllTonTask();
        foreach (var item in allTask)
        {
            if (item.Type == TonChainTaskType.Resend)
            {
                // todo: do task and backwork
            }
        }
    }

    private async Task TonIndexer()
    {
        var tonIndexer = await _tonHelper.GetTonIndexerFromStorage();

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
                            await TonResendTxHandle(tx);
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
            await _tonHelper.StorageTonIndexer(currentIndexer);
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
            var tonTask = await _tonHelper.GetTonTask(forwardMessage.MessageId);
            if (tonTask is { Type: TonChainTaskType.Resend })
            {
                var resendMessage = tonTask.Convert<ResendTonBaseArgs>();
                await _tonHelper.DeleteTonTask(forwardMessage.MessageId);
                _logger.LogInformation(
                    $"[Ton indexer] Resend messageId:{forwardMessage.MessageId} hash:{resendMessage.TargetTxHash} has confirmed, the confirm transaction hash is:{tx.Hash}");
            }
            
            // todo:delete transaction check job
        }
        else
        {
            _logger.LogWarning(
                $"[Ton indexer] AnalysisForwardTransaction Get Null, CrossChainToTonTransactionDto is:{JsonConvert.SerializeObject(tx)}");
        }
    }

    private async Task TonResendTxHandle(CrossChainToTonTransactionDto tx)
    {
        var resendMessage = _tonHelper.AnalysisResendTransaction(tx);
        if (resendMessage != null)
        {
            var tonTask = await _tonHelper.GetTonTask(resendMessage.MessageId);
            if (tonTask is { Type: TonChainTaskType.Resend })
            {
                var resendTask = tonTask.Convert<ResendTonBaseArgs>();
                _logger.LogWarning(
                    $"[Ton indexer] exist resend task, messageId:{resendMessage.MessageId}, block time compare:{resendTask.TargetBlockGeneratorTime}-{tx.BlockTime} resend time compare:{resendTask.ResendTime}-{resendMessage.ResendTime}");
            }
            
            var rampMessageData = await _rampMessageProvider.GetAsync(resendMessage.MessageId);
            if (rampMessageData == null)
            {
                _logger.LogWarning(
                    $"[Ton indexer] resend task, messageId:{resendMessage.MessageId} not find in system, block time:{tx.BlockTime} resend time:{resendMessage.ResendTime}");
                return;
            }
            
            var newTonTask = new ResendTonBaseArgs();
            newTonTask.MessageId = resendMessage.MessageId;
            newTonTask.TargetBlockHeight = tx.SeqNo;
            newTonTask.TargetTxHash = tx.Hash;
            newTonTask.TargetBlockGeneratorTime = tx.BlockTime;
            newTonTask.ResendTime = resendMessage.ResendTime;
            newTonTask.Status = ResendStatus.WaitConsensus;
            await _tonHelper.StorageTonTask(newTonTask.MessageId, new TonChainTaskDto(newTonTask));
            _logger.LogInformation(
                $"[Ton indexer] received resend transaction messageId:{resendMessage.MessageId}, hash:{resendMessage.Hash}, block time:{tx.BlockTime}, resend time:{newTonTask.ResendTime}");

            rampMessageData.ResendTransactionBlockTime =
                DateTimeOffset.FromUnixTimeSeconds(newTonTask.TargetBlockGeneratorTime).DateTime;
            rampMessageData.NextCommitDelayTime = (int) resendMessage.ResendTime;
            await _requestScheduler.Resend(rampMessageData);
            // todo: open task and recheck backwork
        }
        else
        {
            _logger.LogInformation(
                $"[Ton indexer] AnalysisResendTransaction Get Null, CrossChainToTonTransactionDto is:{JsonConvert.SerializeObject(tx)}");
        }
    }
}