using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Dtos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Aetherlink.PriceServer.Common;

public interface IHttpService
{
    Task<T> GetAsync<T>(string url, CancellationToken ctx);
    Task<T> GetAsync<T>(string url, object data, CancellationToken cancellationToken);
    Task<T> GetAsync<T>(string url, IDictionary<string, string> headers, CancellationToken ctx);
}

public class HttpService : IHttpService
{
    private readonly ILogger<HttpService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public HttpService(IHttpClientFactory httpClientFactory, ILogger<HttpService> logger)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<T> GetAsync<T>(string url, CancellationToken ctx)
        => JsonConvert.DeserializeObject<T>(await _httpClientFactory.CreateClient().GetStringAsync(url, ctx));

    public async Task<T> GetAsync<T>(string url, object data, CancellationToken ctx)
        => JsonConvert.DeserializeObject<T>(await (await _httpClientFactory.CreateClient().SendAsync(new()
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(url),
            Content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8,
                NetworkConstants.JsonMediaType)
        }, ctx)).Content.ReadAsStringAsync(ctx));

    public async Task<T> GetAsync<T>(string url, IDictionary<string, string> headers,
        CancellationToken ctx)
    {
        var client = _httpClientFactory.CreateClient();
        headers?.ToList().ForEach(header => client.DefaultRequestHeaders.Add(header.Key, header.Value));
        return JsonConvert.DeserializeObject<T>(await client.GetStringAsync(url, ctx));
    }
}