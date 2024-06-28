using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using AElf.CSharp.Core;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using AetherlinkClient = AetherLink.Worker.Core.AetherLinkServer.AetherLinkServerClient;

namespace AetherLink.Worker.Core.Provider;

public interface IRetryProvider
{
    public Task RetryAsync<T>(T args, bool untilFailed = false, bool backOff = false, long delayDelta = 0)
        where T : JobPipelineArgsBase;

    public Task RetryWithIdAsync<T>(T args, string id, bool untilFailed = false, bool backOff = false,
        long delayDelta = 0);

    Task RetryAsync<T>(OCRContext context, T args, bool untilFailed = false, bool backOff = false, long delay = 0);
}

public class RetryProvider : IRetryProvider, ISingletonDependency
{
    private readonly ILogger<RetryProvider> _logger;
    private readonly ProcessJobOptions _processJobOptions;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ConcurrentDictionary<string, int> _retryCount = new();

    public RetryProvider(IOptionsSnapshot<ProcessJobOptions> processJobOptions, ILogger<RetryProvider> logger,
        IBackgroundJobManager backgroundJobManager)
    {
        _logger = logger;
        _processJobOptions = processJobOptions.Value;
        _backgroundJobManager = backgroundJobManager;
    }

    /// <summary>
    /// RetryProvider will try to retry JobPipelineArgs
    /// By default, a total of RetryCount times will be retried. RetryCount is configured through option.
    /// </summary>
    /// <param name="untilFailed"> untilFailed == true, will keep retrying until failure.</param>
    /// <param name="backOff"> backOff == true, delay time will be avoided exponentially.</param>
    /// <param name="delayDelta"> delayDelta is the basic time for each timeout, the default is 0.</param>
    public async Task RetryAsync<T>(T args, bool untilFailed, bool backOff, long delayDelta = 0)
        where T : JobPipelineArgsBase =>
        await RetryWithIdAsync(args, GenerateRetryId(args), untilFailed, backOff, delayDelta);


    public async Task RetryWithIdAsync<T>(T args, string id, bool untilFailed = false, bool backOff = false,
        long delayDelta = 0)
    {
        _retryCount.TryGetValue(id, out var time);
        if (!untilFailed && time > _processJobOptions.RetryCount) return;

        var delay = backOff ? Math.Pow(delayDelta.Add(time), 2) : delayDelta.Add(time);
        _retryCount[id] = time.Add(1);
        var hangfireId = await _backgroundJobManager.EnqueueAsync(args, delay: TimeSpan.FromSeconds(delay));
        _logger.LogInformation(
            "Task {id} will be executed in {delay} seconds by {hangfireId}. The task has been executed {times} times.",
            id, delay, hangfireId, _retryCount[id]);
    }

    public async Task RetryAsync<T>(OCRContext context, T obj, bool untilFailed = false, bool backOff = false,
        long delay = 0) => await RetryWithIdAsync(obj, GenerateRetryId(context), untilFailed, backOff, delay);

    private string GenerateRetryId<T>(T args) where T : JobPipelineArgsBase
        => IdGeneratorHelper.GenerateId(args.ChainId, args.RequestId, args.Epoch, args.RoundId);

    private string GenerateRetryId(OCRContext context)
        => IdGeneratorHelper.GenerateId(context.ChainId, context.RequestId, context.Epoch, context.RoundId);
}