using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IRampMessageProvider
{
    public Task SetAsync(RampMessageDto msg);
    public Task<RampMessageDto> GetAsync(string messageId);
}

public class RampMessageProvider : IRampMessageProvider, ITransientDependency
{
    private readonly IStorageProvider _storageProvider;
    private readonly ILogger<RampMessageProvider> _logger;

    public RampMessageProvider(IStorageProvider storageProvider, ILogger<RampMessageProvider> logger)
    {
        _logger = logger;
        _storageProvider = storageProvider;
    }

    public async Task SetAsync(RampMessageDto msg)
    {
        var key = GenerateRampMessageId(msg.MessageId);

        _logger.LogDebug("[RampMessageProvider] Start to set {key}, data:{data}", key, msg.Data);

        await _storageProvider.SetAsync(key, msg);
    }

    public async Task<RampMessageDto> GetAsync(string messageId) =>
        await _storageProvider.GetAsync<RampMessageDto>(GenerateRampMessageId(messageId));

    private static string GenerateRampMessageId(string messageId)
        => IdGeneratorHelper.GenerateId(RedisKeyConstants.RampMessageKey, messageId);
}