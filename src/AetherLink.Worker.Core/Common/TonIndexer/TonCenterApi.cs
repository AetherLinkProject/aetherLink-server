using System;
using System.Net.Http;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Common.TonIndexer;

public class TonCenterApi : TonIndexerBase, ISingletonDependency
{
    private readonly TonCenterProviderApiConfig _apiConfig;
    private readonly IHttpClientFactory _clientFactory;
    private readonly TonCenterRequestLimit _requestLimit;

    public TonCenterApi(IOptionsSnapshot<TonCenterProviderApiConfig> snapshotConfig,
        IOptionsSnapshot<TonPublicConfigOptions> tonPublicOptions, IHttpClientFactory clientFactory,
        ILogger<TonCenterApi> logger) : base(tonPublicOptions, logger)
    {
        _apiConfig = snapshotConfig.Value;
        _clientFactory = clientFactory;

        var limitCount = string.IsNullOrEmpty(_apiConfig.ApiKey)
            ? _apiConfig.NoApiKeyPerSecondRequestLimit
            : _apiConfig.ApiKeyPerSecondRequestLimit;
        _requestLimit = new TonCenterRequestLimit(limitCount);

        ProviderName = TonStringConstants.TonCenter;
        ApiWeight = _apiConfig.Weight;
    }

    public override Task<bool> TryGetRequestAccess()
    {
        return Task.FromResult(_requestLimit.TryGetAccess());
    }

    protected override string AssemblyUrl(string path)
    {
        return
            $"{_apiConfig.Url}{(_apiConfig.Url.EndsWith("/") ? "" : "/")}{(path.StartsWith("/") ? path.Substring(1) : path)}";
    }

    protected override HttpClient CreateClient()
    {
        var client = _clientFactory.CreateClient();
        if (!string.IsNullOrEmpty(_apiConfig.ApiKey))
        {
            client.DefaultRequestHeaders.Add("X-Api-Key", _apiConfig.ApiKey);
        }

        client.DefaultRequestHeaders.Add("accept", "application/json");

        return client;
    }
}

public class TonCenterRequestLimit
{
    private readonly object _lock = new object();
    private readonly int _perSecondLimit;
    private long _latestExecuteTime;
    private int _latestSecondExecuteCount;

    public TonCenterRequestLimit(int perSecondLimit)
    {
        _perSecondLimit = perSecondLimit;
    }

    public bool TryGetAccess()
    {
        var dtNow = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        lock (_lock)
        {
            if (_latestExecuteTime == dtNow)
            {
                if (_perSecondLimit >= _latestSecondExecuteCount)
                {
                    return false;
                }

                _latestSecondExecuteCount += 1;
                return true;
            }

            if (dtNow > _latestExecuteTime)
            {
                _latestExecuteTime = dtNow;
                _latestSecondExecuteCount = 1;
            }

            return true;
        }
    }
}