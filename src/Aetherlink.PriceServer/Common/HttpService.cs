using System.Web;
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
    {
        url += "?" + string.Join("&", data.GetType().GetProperties().Select(property =>
            property.GetValue(data) is IList<string> list && list.Any()
                ? string.Join("&", list.Select(item => $"{property.Name}={item}"))
                : $"{property.Name}={HttpUtility.UrlEncode(property.GetValue(data)?.ToString())}"));

        return JsonConvert.DeserializeObject<T>(await (await _httpClientFactory.CreateClient().SendAsync(new()
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(url),
            Headers = { { "accept", "text/plain" }, { "X-Requested-With", "XMLHttpRequest" } }
        }, ctx)).Content.ReadAsStringAsync(ctx));
    }

    public async Task<T> GetAsync<T>(string url, IDictionary<string, string> headers,
        CancellationToken ctx)
    {
        var client = _httpClientFactory.CreateClient();
        headers?.ToList().ForEach(header => client.DefaultRequestHeaders.Add(header.Key, header.Value));
        return JsonConvert.DeserializeObject<T>(await client.GetStringAsync(url, ctx));
    }
}