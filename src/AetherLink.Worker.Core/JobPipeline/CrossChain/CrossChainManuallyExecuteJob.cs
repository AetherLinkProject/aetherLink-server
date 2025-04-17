using System;
using System.Threading;
using System.Threading.Tasks;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.JobPipeline.CrossChain;

public class CrossChainManuallyExecuteJob : IAsyncBackgroundJob<CrossChainRequestManuallyExecuteJobArgs>,
    ITransientDependency
{
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ICrossChainRequestProvider _crossChainRequestProvider;
    private readonly ILogger<CrossChainRequestManuallyExecuteJobArgs> _logger;

    public CrossChainManuallyExecuteJob(ILogger<CrossChainRequestManuallyExecuteJobArgs> logger,
        ICrossChainRequestProvider crossChainRequestProvider, IBackgroundJobManager backgroundJobManager)
    {
        _logger = logger;
        _backgroundJobManager = backgroundJobManager;
        _crossChainRequestProvider = crossChainRequestProvider;
    }

    public async Task ExecuteAsync(CrossChainRequestManuallyExecuteJobArgs args)
    {
        try
        {
            var rampMessageData = await _crossChainRequestProvider.GetAsync(args.MessageId);
            if (rampMessageData == null)
            {
                var messageId128 = _crossChainRequestProvider.Ensure128BytesMessageId(args.MessageId);
                rampMessageData = await _crossChainRequestProvider.GetAsync(messageId128);

                if (rampMessageData == null)
                {
                    _logger.LogWarning($"[CrossChainManuallyExecute] {args.MessageId} not exist");
                    return;
                }
            }

            // reset RequestReceiveTime to ManuallyExecute transaction block time
            rampMessageData.RequestReceiveTime = DateTimeOffset.FromUnixTimeMilliseconds(args.StartTime).DateTime;
            // reset round back to zero
            rampMessageData.ReportContext.RoundId = 0;
            await _crossChainRequestProvider.SetAsync(rampMessageData);
            await _backgroundJobManager.EnqueueAsync(new CrossChainRequestStartArgs
                { ReportContext = rampMessageData.ReportContext });

            _logger.LogInformation($"[CrossChainManuallyExecute] Request {args.MessageId} manually execute.");
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[CrossChainManuallyExecute] {args.MessageId} manually execute failed");
        }
    }
}