using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace AetherLink.Server.Grains;

[DependsOn]
public class AetherLinkServerGrainsModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        base.ConfigureServices(context);
        Configure<AbpAutoMapperOptions>(options => options.AddMaps<AetherLinkServerGrainsModule>());
    }
}