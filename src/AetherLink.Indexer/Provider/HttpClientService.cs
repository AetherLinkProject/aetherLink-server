using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AetherLink.Indexer.Constants;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AetherLink.Indexer.Provider;

public interface IHttpClientService
{
    Task<T> GetAsync<T>(string url, CancellationToken cancellationToken);
    Task<string> GetAsync(string url, CancellationToken cancellationToken);
}

public class HttpClientService : IHttpClientService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpClientService> _logger;

    public HttpClientService(IHttpClientFactory httpClientFactory, ILogger<HttpClientService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(NetworkConstants.DefaultTimout);
        var response = await client.GetStringAsync(url, cancellationToken);

        return JsonConvert.DeserializeObject<T>(response);
    }

    public async Task<string> GetAsync(string url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(NetworkConstants.DefaultTimout);
        return await client.GetStringAsync(url, cancellationToken);
    }
}