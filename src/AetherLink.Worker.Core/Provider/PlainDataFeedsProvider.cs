using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IPlainDataFeedsProvider
{
    public Task<string> RequestPlainDataAsync(string url);
}

public class PlainDataFeedsProvider : IPlainDataFeedsProvider, ITransientDependency
{
    private readonly ILogger<PlainDataFeedsProvider> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public PlainDataFeedsProvider(IHttpClientFactory httpClientFactory, ILogger<PlainDataFeedsProvider> logger)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> RequestPlainDataAsync(string url)
    {
        try
        {
            _logger.LogDebug("Starting to request {url}", url);
            var resp = await _httpClientFactory.CreateClient().SendAsync(new()
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(url),
                Headers = { { "accept", "text/plain" }, { "X-Requested-With", "XMLHttpRequest" } }
            });

            if (resp.StatusCode != HttpStatusCode.OK) return "";

            return await resp.Content.ReadAsStringAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Query auth url {url} failed");
            return "";
        }
    }
}