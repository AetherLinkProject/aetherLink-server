using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Nest;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Common.TonIndexer;

public sealed class TonCenterApi:TonIndexerBase,ISingletonDependency
{
    private readonly TonCenterApiConfig _apiConfig;
    private readonly IHttpClientFactory _clientFactory;
    private readonly TonCenterRequestLimit _requestLimit;
    
    public TonCenterApi(IOptionsSnapshot<IConfiguration> snapshotConfig, TonHelper tonHelper, IHttpClientFactory  clientFactory):base(tonHelper)
    {
         _apiConfig = snapshotConfig.Value.GetSection("Chains:ChainInfos:Ton:Indexer:TonCenter").Get<TonCenterApiConfig>();
         _clientFactory = clientFactory;

         var limitCount = string.IsNullOrEmpty(_apiConfig.ApiKey)
             ? _apiConfig.NoApiKeyPerSecondRequestLimit
             : _apiConfig.ApiKeyPerSecondRequestLimit;

         _requestLimit = new TonCenterRequestLimit(limitCount);
         
        _apiWeight = _apiConfig.Weight;
    }


    public override Task<bool> TryGetRequestAccess()
    {
        return Task.FromResult(_requestLimit.TryGetAccess());
    }

    protected override string AssemblyUrl(string path)
    {
        return  $"{_apiConfig.Url}{(_apiConfig.Url.EndsWith("/")?"":"/")}{(path.StartsWith("/")? path.Substring(1): path)}";
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
            if (_latestExecuteTime == dtNow )
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

public class TonCenterApiConfig
{
    public string Url { get; set; }
    public int Weight { get; set; }
    public string ApiKey { get; set; }
    
    public int ApiKeyPerSecondRequestLimit { get; set; }
    
    public int NoApiKeyPerSecondRequestLimit { get; set; }
}