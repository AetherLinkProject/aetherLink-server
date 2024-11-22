using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AetherLink.Indexer.Constants;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp;

namespace AetherLink.Indexer.Provider;

public interface IHttpClientService
{
    Task<T> GetAsync<T>(string url, CancellationToken cancellationToken);
    Task<string> GetAsync(string url, CancellationToken cancellationToken);
    Task<T> GetAsync<T>(string url, IDictionary<string, string> headers, CancellationToken cancellationToken);
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

    public async Task<T> GetAsync<T>(string url, IDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        if (headers == null)
        {
            return await GetAsync<T>(url, cancellationToken);
        }

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(NetworkConstants.DefaultTimout);
        foreach (var keyValuePair in headers)
        {
            client.DefaultRequestHeaders.Add(keyValuePair.Key, keyValuePair.Value);
        }

        var response = await client.GetStringAsync(url, cancellationToken);
        _logger.LogDebug("[HttpClientService] Response string: {str}", response);
        return JsonConvert.DeserializeObject<T>(response);
    }
}