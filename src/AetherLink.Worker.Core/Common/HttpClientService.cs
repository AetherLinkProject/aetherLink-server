using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Consts;
using AetherLink.Worker.Core.Dtos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp;

namespace AetherLink.Worker.Core.Common;

public interface IHttpClientService
{
    Task<T> GetAsync<T>(string url);
    Task<T> GetAsync<T>(string url, IDictionary<string, string> headers);
    Task<T> PostAsync<T>(string url);
    Task<T> PostAsync<T>(string url, object paramObj);
    Task<T> PostAsync<T>(string url, object paramObj, Dictionary<string, string> headers);
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

    public async Task<T> GetAsync<T>(string url)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(HttpConst.DefaultTimout);
        var response = await client.GetStringAsync(url);

        return JsonConvert.DeserializeObject<T>(response);
    }

    public async Task<T> GetAsync<T>(string url, Dictionary<string, string> headers)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(HttpConst.DefaultTimout);
        foreach (var keyValuePair in headers)
        {
            client.DefaultRequestHeaders.Add(keyValuePair.Key, keyValuePair.Value);
        }

        var response = await client.GetStringAsync(url);
        return JsonConvert.DeserializeObject<T>(response);
    }

    public async Task<T> GetAsync<T>(string url, IDictionary<string, string> headers)
    {
        if (headers == null)
        {
            return await GetAsync<T>(url);
        }

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(HttpConst.DefaultTimout);
        foreach (var keyValuePair in headers)
        {
            client.DefaultRequestHeaders.Add(keyValuePair.Key, keyValuePair.Value);
        }

        var response = await client.GetStringAsync(url);
        _logger.LogDebug("[HttpClientService] Response string: {str}", response);
        return JsonConvert.DeserializeObject<T>(response);
    }

    public async Task<T> PostAsync<T>(string url)
    {
        return await PostJsonAsync<T>(url, null, null);
    }

    public async Task<T> PostAsync<T>(string url, object paramObj)
    {
        return await PostJsonAsync<T>(url, paramObj, null);
    }

    public async Task<T> PostAsync<T>(string url, object paramObj, Dictionary<string, string> headers)
    {
        return await PostJsonAsync<T>(url, paramObj, headers);
    }

    private async Task<T> PostJsonAsync<T>(string url, object paramObj, Dictionary<string, string> headers)
    {
        var requestInput = paramObj == null ? string.Empty : JsonConvert.SerializeObject(paramObj, Formatting.None);

        var requestContent = new StringContent(
            requestInput,
            Encoding.UTF8,
            MediaTypeNames.Application.Json);

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(HttpConst.DefaultTimout);

        if (headers is { Count: > 0 })
        {
            foreach (var header in headers)
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        var response = await client.PostAsync(url, requestContent);
        var content = await response.Content.ReadAsStringAsync();

        if (!ResponseSuccess(response.StatusCode))
        {
            _logger.LogError("Response not success, url:{url}, code:{code}, message: {message}, params:{param}",
                url, response.StatusCode, content, JsonConvert.SerializeObject(paramObj));

            throw new UserFriendlyException(content, ((int)response.StatusCode).ToString());
        }

        return JsonConvert.DeserializeObject<T>(content);
    }

    private async Task<T> PostFormAsync<T>(string url, Dictionary<string, string> paramDic,
        Dictionary<string, string> headers)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(HttpConst.DefaultTimout);

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

        var response = await client.PostAsync(url, new FormUrlEncodedContent(param));
        var content = await response.Content.ReadAsStringAsync();

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