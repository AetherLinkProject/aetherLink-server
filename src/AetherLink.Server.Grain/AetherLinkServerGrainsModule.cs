using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace AetherLink.Server.Grains;

[DependsOn(typeof(AbpAutoMapperModule))]
public class AetherLinkServerGrainsModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<AetherLinkServerGrainsModule>(); });

        // base.ConfigureServices(context);
        // Configure<AbpAutoMapperOptions>(options => options.AddMaps<AetherLinkServerGrainsModule>());
    }
}