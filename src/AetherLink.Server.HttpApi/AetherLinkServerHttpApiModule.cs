using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace AetherLink.Server.HttpApi;

[DependsOn(
    typeof(AbpAspNetCoreMvcModule)
)]
public class AetherLinkServerHttpApiModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<AetherLinkServerHttpApiModule>(); });
    }
}