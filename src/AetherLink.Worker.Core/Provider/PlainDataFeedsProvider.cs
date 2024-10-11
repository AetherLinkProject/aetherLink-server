using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AetherLink.Worker.Core.Dtos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
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

    [ExceptionHandler(typeof(Exception), TargetType = typeof(PlainDataFeedsProvider),
        MethodName = nameof(HandleException))]
    public virtual async Task<string> RequestPlainDataAsync(string url)
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

    private async Task<string> GetSortedResponseContentAsStringAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var authResponse = JsonConvert.DeserializeObject<AuthResponseDto>(content);
        authResponse.Keys = authResponse.Keys.OrderBy(a => a.Kid).ToList();
        return JsonConvert.SerializeObject(authResponse,
            new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });

        // var jsonObject = JsonConvert.DeserializeObject<JObject>(content);
        // var sortedObject = SortJToken(jsonObject);
        // return JsonConvert.SerializeObject(sortedObject, Formatting.Indented);
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

    #region Exception Handing

    public async Task<FlowBehavior> HandleException(Exception ex, string url)
    {
        _logger.LogError(ex, $"Query auth url {url} failed");
        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = ""
        };
    }

    #endregion
}