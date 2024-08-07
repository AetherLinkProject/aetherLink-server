using System.Threading.Tasks;
using AetherLink.AIServer.Core.Dtos;
using AetherLink.Contracts.AIFeeds;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AetherLink.AIServer.Core.Enclave;

public interface IEnclaveManager
{
    Task CreateAsync(AIRequestDto request);
    Task ProcessAsync(string requestId);
    Task FinishAsync(string requestId);
}

public class EnclaveManager : IEnclaveManager, ISingletonDependency
{
    private readonly ILogger<EnclaveManager> _logger;

    public EnclaveManager(ILogger<EnclaveManager> logger)
    {
        _logger = logger;
    }

    public async Task CreateAsync(AIRequestDto request)
    {
        var description = Description.Parser.ParseFrom(ByteString.FromBase64(request.Commitment));
        if (description.Type == DescriptionType.String)
            _logger.LogDebug($"[EnclaveManager] {description.Detail.ToStringUtf8()}");
    }

    public Task ProcessAsync(string requestId)
    {
        throw new System.NotImplementedException();
    }

    public Task FinishAsync(string requestId)
    {
        throw new System.NotImplementedException();
    }
}