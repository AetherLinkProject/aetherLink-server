using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Consts;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IRequestProvider
{
    public Task SetAsync(RequestDto request);
    Task<RequestDto> GetAsync<T>(T arg) where T : JobPipelineArgsBase;
}

public class RequestProvider : IRequestProvider, ITransientDependency
{
    private readonly IStorageProvider _storageProvider;
    private readonly ILogger<RequestProvider> _logger;

    public RequestProvider(IStorageProvider storageProvider, ILogger<RequestProvider> logger)
    {
        _storageProvider = storageProvider;
        _logger = logger;
    }

    public async Task SetAsync(RequestDto request)
    {
        var key = GetJobRequestKey(request.ChainId, request.RequestId);
        _logger.LogDebug("[RequestProvider] Start to set request {key}. state:{state}", key, request.State);

        await _storageProvider.SetAsync(key, request);
    }

    public async Task<RequestDto> GetAsync<T>(T arg) where T : JobPipelineArgsBase
        => await _storageProvider.GetAsync<RequestDto>(GetJobRequestKey(arg.ChainId, arg.RequestId));

    private static string GetJobRequestKey(string chainId, string requestId)
        => IdGeneratorHelper.GenerateId(RedisKeyConst.JobRequestRedisKey, chainId, requestId);
}