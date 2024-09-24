using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Common.TonIndexer;

public sealed class GetBlockApi:TonIndexerBase,ISingletonDependency
{
    private readonly TonCenterApiConfig _apiConfig;
    private readonly IHttpClientFactory _clientFactory;
    
    public int ApiWeight { get; }

    public GetBlockApi(IOptionsSnapshot<IConfiguration> snapshotConfig, TonHelper tonHelper,
        IHttpClientFactory clientFactory):base(tonHelper)
    {
        _apiConfig = snapshotConfig.Value.GetSection("Chains:ChainInfos:Ton:Indexer:GetBlock").Get<TonCenterApiConfig>();
        _clientFactory = clientFactory;
    }

    public override Task<bool> TryGetRequestAccess()
    {
        throw new System.NotImplementedException();
    }
    
    protected override string AssemblyUrl(string path)
    {
        return $"{_apiConfig.Url}{(_apiConfig.Url.EndsWith("/") ? "" : "/")}{_apiConfig.ApiKey}{(path.StartsWith("/") ? "" : "/")}{path}";
    }

    protected override HttpClient CreateClient()
    {
        var client = _clientFactory.CreateClient();
        return client;
    }
}

public class GetBlockConfig
{
    public string Url { get; set; }
    public int Weight { get; set; }
    public string ApiKey { get; set; }
    public bool FreeApi { get; set; }
}