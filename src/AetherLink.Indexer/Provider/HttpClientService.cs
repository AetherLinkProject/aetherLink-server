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
    Task<T> GetAsync<T>(string url, IDictionary<string, string> headers, CancellationToken cancellationToken);
    Task<T> PostAsync<T>(string url, CancellationToken cancellationToken);
    Task<T> PostAsync<T>(string url, object paramObj, CancellationToken cancellationToken);

    Task<T> PostAsync<T>(string url, object paramObj, Dictionary<string, string> headers,
        CancellationToken cancellationToken);
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

    public async Task<T> GetAsync<T>(string url, Dictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(NetworkConstants.DefaultTimout);
        foreach (var keyValuePair in headers)
        {
            client.DefaultRequestHeaders.Add(keyValuePair.Key, keyValuePair.Value);
        }

        var response = await client.GetStringAsync(url, cancellationToken);
        return JsonConvert.DeserializeObject<T>(response);
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

    public async Task<T> PostAsync<T>(string url, CancellationToken cancellationToken)
    {
        return await PostJsonAsync<T>(url, null, null, cancellationToken);
    }

    public async Task<T> PostAsync<T>(string url, object paramObj, CancellationToken cancellationToken)
    {
        return await PostJsonAsync<T>(url, paramObj, null, cancellationToken);
    }

    public async Task<T> PostAsync<T>(string url, object paramObj, Dictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        return await PostJsonAsync<T>(url, paramObj, headers, cancellationToken);
    }

    private async Task<T> PostJsonAsync<T>(string url, object paramObj, Dictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        var requestInput = paramObj == null ? string.Empty : JsonConvert.SerializeObject(paramObj, Formatting.None);

        var requestContent = new StringContent(
            requestInput,
            Encoding.UTF8,
            MediaTypeNames.Application.Json);

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(NetworkConstants.DefaultTimout);

        if (headers is { Count: > 0 })
        {
            foreach (var header in headers)
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        var response = await client.PostAsync(url, requestContent, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!ResponseSuccess(response.StatusCode))
        {
            _logger.LogError("Response not success, url:{url}, code:{code}, message: {message}, params:{param}",
                url, response.StatusCode, content, JsonConvert.SerializeObject(paramObj));

            throw new UserFriendlyException(content, ((int)response.StatusCode).ToString());
        }

        return JsonConvert.DeserializeObject<T>(content);
    }

    private async Task<T> PostFormAsync<T>(string url, Dictionary<string, string> paramDic,
        Dictionary<string, string> headers, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(NetworkConstants.DefaultTimout);

        if (headers is { Count: > 0 })
        {
            foreach (var header in headers)
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        var param = new List<KeyValuePair<string, string>>();
        if (paramDic is { Count: > 0 })
        {
            param.AddRange(paramDic.ToList());
        }

        var response = await client.PostAsync(url, new FormUrlEncodedContent(param), cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!ResponseSuccess(response.StatusCode))
        {
            _logger.LogError("Response not success, url:{url}, code:{code}, message: {message}, params:{param}",
                url, response.StatusCode, content, JsonConvert.SerializeObject(paramDic));

            throw new UserFriendlyException(content, ((int)response.StatusCode).ToString());
        }

        return JsonConvert.DeserializeObject<T>(content);
    }

    private bool ResponseSuccess(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.OK or HttpStatusCode.NoContent;
}