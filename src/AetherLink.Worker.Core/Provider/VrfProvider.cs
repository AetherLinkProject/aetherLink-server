using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Dtos;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IVrfProvider
{
    public Task SetAsync(VrfJobDto job);
    public Task<VrfJobDto> GetAsync(string chainId, string requestId);
}

public class VrfProvider : IVrfProvider, ITransientDependency
{
    private readonly IStorageProvider _storageProvider;
    private readonly ILogger<VrfProvider> _logger;

    public VrfProvider(IStorageProvider storageProvider, ILogger<VrfProvider> logger)
    {
        _storageProvider = storageProvider;
        _logger = logger;
    }

    public async Task SetAsync(VrfJobDto job)
    {
        var key = IdGeneratorHelper.GenerateVrfJobRedisId(job.ChainId, job.RequestId);

        _logger.LogDebug("[VrfProvider] Start to set job {key}. status:{status}", key, job.Status);

        await _storageProvider.SetAsync(key, job);
    }

    public async Task<VrfJobDto> GetAsync(string chainId, string requestId)
        => await _storageProvider.GetAsync<VrfJobDto>(IdGeneratorHelper.GenerateVrfJobRedisId(chainId, requestId));
}