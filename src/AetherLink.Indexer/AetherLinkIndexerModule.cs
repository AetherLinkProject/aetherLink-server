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
        context.Services.AddTransient<IAeFinderProvider, AeFinderProvider>();
    }
}