using System;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Common.TonIndexer;
using AetherLink.Worker.Core.Constants;
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

    public TonIndexerWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        TonIndexerRouter tonIndexerRouter, TonHelper tonHelper, IBackgroundJobManager backgroundJobManager, ILogger<TonIndexerWorker> logger) : base(timer,
        serviceScopeFactory)
    {
        _tonHelper = tonHelper;
        _logger = logger;
        _tonIndexerRouter = tonIndexerRouter;
        _backgroundJobManager = backgroundJobManager;
        
        timer.Period = 1000 * 3; // 
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
                            var forwardMessage = _tonHelper.AnalysisForwardTransaction(tx);
                            if (forwardMessage != null)
                            {
                                // todo: check resend status
                            }

                            break;
                        case TonOpCodeConstants.ReceiveTx:
                            
                            break;
                        case TonOpCodeConstants.ResendTx:
                            var resendMsg = _tonHelper.AnalysisResendTransaction(tx);
                            // todo:check
                            
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
        }
    }
}