using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Consts;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IDataMessageProvider
{
    public Task SetAsync(DataMessageDto request);
    public Task<DataMessageDto> GetAsync<T>(T arg) where T : JobPipelineArgsBase;
}

public class DataMessageProvider : IDataMessageProvider, ITransientDependency
{
    private readonly IStorageProvider _storageProvider;
    private readonly ILogger<DataMessageProvider> _logger;

    public DataMessageProvider(IStorageProvider storageProvider, ILogger<DataMessageProvider> logger)
    {
        _storageProvider = storageProvider;
        _logger = logger;
    }

    public async Task SetAsync(DataMessageDto msg)
    {
        var key = GenerateDataMessageRedisKey(msg.ChainId, msg.RequestId, msg.Epoch);
        _logger.LogDebug("[DataMessageProvider] Start to set {key}, data:{state}", key, msg.Data);

        await _storageProvider.SetAsync(key, msg);
    }

    public async Task<DataMessageDto> GetAsync<T>(T arg) where T : JobPipelineArgsBase
    {
        var key = GenerateDataMessageRedisKey(arg.ChainId, arg.RequestId, arg.Epoch);
        _logger.LogDebug("[DataMessageProvider] Get data {key}.", key);

        return await _storageProvider.GetAsync<DataMessageDto>(key);
    }

    private static string GenerateDataMessageRedisKey(string chainId, string requestId, long epoch)
        => IdGeneratorHelper.GenerateId(RedisKeyConst.DataMessageRedisKey, chainId, requestId, epoch);
}