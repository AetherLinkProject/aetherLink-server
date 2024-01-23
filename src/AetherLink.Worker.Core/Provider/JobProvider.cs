using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IJobProvider
{
    public Task SetAsync(JobDto job);
    Task<JobDto> GetAsync<T>(T arg) where T : JobPipelineArgsBase;
}

public class JobProvider : IJobProvider, ITransientDependency
{
    private readonly IStorageProvider _storageProvider;
    private readonly ILogger<JobProvider> _logger;

    public JobProvider(IStorageProvider storageProvider, ILogger<JobProvider> logger)
    {
        _storageProvider = storageProvider;
        _logger = logger;
    }

    public async Task SetAsync(JobDto job)
    {
        var key = GetJobRequestKey(job.ChainId, job.RequestId);
        _logger.LogDebug("[JobProvider] Start to set job {key}. state:{state}", key, job.State);

        await _storageProvider.SetAsync(key, job);
    }

    public async Task<JobDto> GetAsync<T>(T arg) where T : JobPipelineArgsBase
        => await _storageProvider.GetAsync<JobDto>(GetJobRequestKey(arg.ChainId, arg.RequestId));

    private static string GetJobRequestKey(string chainId, string requestId)
        => IdGeneratorHelper.GenerateId(RedisKeyConstants.JobRedisKey, chainId, requestId);
}