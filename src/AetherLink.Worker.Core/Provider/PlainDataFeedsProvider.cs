using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

            if (resp.StatusCode == HttpStatusCode.OK) return await GetSortedResponseContentAsStringAsync(resp);

            _logger.LogWarning("Request {url} failed, StatusCode:{code}", url, resp.StatusCode);

            return "";
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Query auth url {url} failed");

            return "";
        }
    }

    private async Task<string> GetSortedResponseContentAsStringAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var jsonObject = JsonConvert.DeserializeObject<JObject>(content);
        var sortedObject = SortJToken(jsonObject);
        var sortedJson = JsonConvert.SerializeObject(sortedObject, Formatting.Indented);

        return sortedJson;
    }

    private JObject SortJToken(JObject jObject)
    {
        var sortedDict = new SortedDictionary<string, object>();
        foreach (var kvp in jObject)
        {
            sortedDict[kvp.Key] = SortJToken(kvp.Value);
        }

        return JObject.FromObject(sortedDict);
    }

    private object SortJToken(JToken token)
    {
        switch (token.Type)
        {
            case JTokenType.Object:
                return SortJToken((JObject)token);
            case JTokenType.Array:
                var array = new JArray();
                foreach (var item in token) array.Add(SortJToken(item));
                return array;
            case JTokenType.String:
                return token.ToString();
            case JTokenType.Integer:
                return token.ToObject<long>();
            case JTokenType.Float:
                return token.ToObject<double>();
            case JTokenType.Boolean:
                return token.ToObject<bool>();
            case JTokenType.Null:
                return null;
            default:
                throw new NotSupportedException($"Unsupported JSON token: {token.Type}");
        }
    }
}