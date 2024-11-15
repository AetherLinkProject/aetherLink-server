using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AetherLink.Worker.Core.ChainHandler;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Scheduler;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.JobPipeline.CrossChain;

public class CrossChainReceivedResultCheckJob : IAsyncBackgroundJob<CrossChainReceivedResultCheckJobArgs>,
    ITransientDependency
{
    private readonly IBackgroundJobManager _jobManager;
    private readonly ISchedulerService _schedulerService;
    private readonly ICrossChainRequestProvider _requestProvider;
    private readonly Dictionary<long, IChainReader> _chainReaders;
    private readonly ILogger<CrossChainReceivedResultCheckJob> _logger;

    public CrossChainReceivedResultCheckJob(ILogger<CrossChainReceivedResultCheckJob> logger,
        ICrossChainRequestProvider requestProvider, ISchedulerService schedulerService,
        IEnumerable<IChainReader> chainReaders, IBackgroundJobManager jobManager)
    {
        _logger = logger;
        _jobManager = jobManager;
        _requestProvider = requestProvider;
        _schedulerService = schedulerService;
        _chainReaders = chainReaders.GroupBy(x => x.ChainId).Select(g => g.First())
            .ToDictionary(x => x.ChainId, y => y);
    }

    public async Task ExecuteAsync(CrossChainReceivedResultCheckJobArgs args)
    {
        var reportContext = args.ReportContext;
        var messageId = reportContext.MessageId;
        _logger.LogInformation(
            $"[CrossChain] Get Leader commit {messageId} report result: {args.CommitTransactionId}");

        var crossChainData = await _requestProvider.GetAsync(messageId);
        if (crossChainData == null)
        {
            _logger.LogWarning($"[CrossChain] Get not exist job {messageId} from leader");
            return;
        }

        if (crossChainData.State == CrossChainState.RequestCanceled)
        {
            _logger.LogWarning($"[CrossChain] CrossChain request {messageId} canceled");
            return;
        }

        if (!_chainReaders.TryGetValue(reportContext.TargetChainId, out var reader))
        {
            _logger.LogWarning($"[CrossChain] Unknown target chain id: {reportContext.TargetChainId}");
            return;
        }

        var transactionResult = await reader.GetTransactionResultAsync(args.CommitTransactionId);
        switch (transactionResult.State)
        {
            case TransactionState.Success:
                crossChainData.State = CrossChainState.Committed;
                await _requestProvider.SetAsync(crossChainData);
                _schedulerService.CancelScheduler(crossChainData);
                _logger.LogInformation($"[CrossChain] CrossChain request job {messageId} commit successful.");

                break;
            case TransactionState.Pending:
                _logger.LogInformation($"[CrossChain] CrossChain request job {messageId} commit is pending.");
                await _jobManager.EnqueueAsync(args, delay: TimeSpan.FromSeconds(RetryConstants.CheckResultDelay));

                break;
            case TransactionState.NotExist:
            case TransactionState.Fail:
                _logger.LogWarning($"[CrossChain] CrossChain request job {messageId} commit fail.");
                break;
        }
    }
}