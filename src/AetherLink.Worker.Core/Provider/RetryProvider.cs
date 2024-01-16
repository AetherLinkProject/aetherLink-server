using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IRetryProvider
{
    public Task RetryAsync<T>(T args) where T : JobPipelineArgsBase;
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

    public async Task RetryAsync<T>(T args) where T : JobPipelineArgsBase
    {
        var id = IdGeneratorHelper.GenerateId(args.ChainId, args.RequestId, args.Epoch, args.RoundId);
        _retryCount.TryGetValue(id, out var time);
        if (time < _processJobOptions.RetryCount)
        {
            _logger.LogWarning("Key {id} Retry {times} times ", id, time);
            _retryCount[id] = time + 1;
            await _backgroundJobManager.EnqueueAsync(args,
                delay: TimeSpan.FromSeconds(_retryCount[id] * _retryCount[id]));
        }
    }
}