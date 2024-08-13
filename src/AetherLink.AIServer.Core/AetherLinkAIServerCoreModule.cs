using AetherLink.AIServer.Core.Providers;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace AetherLink.AIServer.Core;

[DependsOn(
    typeof(AbpAutoMapperModule)
)]
public class AetherLinkAIServerCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<AetherLinkAIServerCoreModule>(); });
        context.Services.AddTransient<IRequestProvider, RequestProvider>();
        context.Services.AddTransient<IStorageProvider, StorageProvider>();
    }
}