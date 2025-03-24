using AetherLink.Indexer.Provider;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace AetherLink.Indexer;

public class AetherLinkIndexerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        Configure<AeFinderOptions>(configuration.GetSection("AeFinder"));
        Configure<TonIndexerOption>(configuration.GetSection("TonIndexer"));
        Configure<EvmIndexerOptionsMap>(configuration.GetSection("EvmNetworks"));
        context.Services.AddTransient<IAeFinderProvider, AeFinderProvider>();
        context.Services.AddTransient<ITonIndexerProvider, TonIndexerProvider>();
        context.Services.AddHttpClient();
        context.Services.AddScoped<IHttpClientService, HttpClientService>();
    }
}