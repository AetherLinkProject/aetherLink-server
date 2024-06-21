using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IDataMessageProvider
{
    public Task SetAsync(DataMessageDto request);
    public Task SetAsync(PlainDataFeedsDto msg);
    public Task<DataMessageDto> GetAsync<T>(T arg) where T : JobPipelineArgsBase;
    public Task<PlainDataFeedsDto> GetPlainDataFeedsAsync<T>(T arg) where T : JobPipelineArgsBase;
    public Task<PlainDataFeedsDto> GetPlainDataFeedsAsync(string chainId, string requestId);
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
        var key = GenerateDataMessageId(msg.ChainId, msg.RequestId, msg.Epoch);

        _logger.LogDebug("[DataMessageProvider] Start to set {key}, data:{data}", key, msg.Data);

        await _storageProvider.SetAsync(key, msg);
    }

    public async Task SetAsync(PlainDataFeedsDto msg) =>
        await _storageProvider.SetAsync(GeneratePlainDataFeedsId(msg.ChainId, msg.RequestId), msg);

    public async Task<DataMessageDto> GetAsync<T>(T arg) where T : JobPipelineArgsBase =>
        await _storageProvider.GetAsync<DataMessageDto>(GenerateDataMessageId(arg.ChainId, arg.RequestId, arg.Epoch));

    public async Task<PlainDataFeedsDto> GetPlainDataFeedsAsync<T>(T arg) where T : JobPipelineArgsBase
        => await GetPlainDataFeedsAsync(arg.ChainId, arg.RequestId);

    public async Task<PlainDataFeedsDto> GetPlainDataFeedsAsync(string chainId, string requestId)
        => await _storageProvider.GetAsync<PlainDataFeedsDto>(GeneratePlainDataFeedsId(chainId, requestId));

    private static string GenerateDataMessageId(string chainId, string requestId, long epoch)
        => IdGeneratorHelper.GenerateId(RedisKeyConstants.DataMessageKey, chainId, requestId, epoch);

    private static string GeneratePlainDataFeedsId(string chainId, string requestId)
        => IdGeneratorHelper.GenerateId(RedisKeyConstants.PlainDataFeedsKey, chainId, requestId);
}