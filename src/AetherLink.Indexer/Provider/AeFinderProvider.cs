using System.Collections.Generic;
using System.Threading.Tasks;
using AetherLink.Indexer.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AetherLink.Indexer.Provider;

public interface IAeFinderProvider
{
    Task<List<ChainItemDto>> GetChainSyncStateAsync();
}

public class AeFinderProvider : IAeFinderProvider
{
    private readonly AeFinderOptions _options;
    private readonly IHttpClientService _httpClient;
    private readonly ILogger<AeFinderProvider> _logger;

    public AeFinderProvider(IOptionsSnapshot<AeFinderOptions> options, IHttpClientService httpClient,
        ILogger<AeFinderProvider> logger)
    {
        _logger = logger;
        _options = options.Value;
        _httpClient = httpClient;
    }

    public async Task<List<ChainItemDto>> GetChainSyncStateAsync()
    {
        var result = await _httpClient.GetAsync<AeFinderSyncStateDto>(_options.BaseUrl + _options.SyncStateUri, new());
        return result.CurrentVersion.Items;
    }
}