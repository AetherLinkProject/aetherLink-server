using AetherLink.Indexer;
using AetherLink.Server.Grains;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace AetherLink.Server.Silo;

[DependsOn(typeof(AbpAutofacModule),
    typeof(AetherLinkIndexerModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AetherLinkServerGrainsModule)
)]
public class AetherLinkServerOrleansSiloModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHostedService<AetherLinkServerHostedService>();
    }
}