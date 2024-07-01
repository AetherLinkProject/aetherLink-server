using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IJobProvider
{
    public Task SetAsync(JobDto job);
    Task<JobDto> GetAsync<T>(T arg) where T : JobPipelineArgsBase;
    Task<JobDto> GetAsync(OCRContext context);
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
        var key = IdGeneratorHelper.GenerateJobRequestRedisId(job.ChainId, job.RequestId);

        _logger.LogDebug("[JobProvider] Start to set job {key}. state:{state}", key, job.State);

        await _storageProvider.SetAsync(key, job);
    }

    public async Task<JobDto> GetAsync<T>(T arg) where T : JobPipelineArgsBase
        => await _storageProvider.GetAsync<JobDto>(
            IdGeneratorHelper.GenerateJobRequestRedisId(arg.ChainId, arg.RequestId));

    public async Task<JobDto> GetAsync(OCRContext context) => await _storageProvider.GetAsync<JobDto>(
        IdGeneratorHelper.GenerateJobRequestRedisId(context.ChainId, context.RequestId));
}