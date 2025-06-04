using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;
using AetherLink.Server.HttpApi.Reporter;

namespace AetherLink.Server.HttpApi;

[DependsOn(
    typeof(AbpAspNetCoreMvcModule)
)]
public class AetherLinkServerHttpApiModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<AetherLinkServerHttpApiModule>(); });
        context.Services.AddSingleton<IJobsReporter, JobsReporter>();
        context.Services.AddSingleton<ICrossChainReporter, CrossChainReporter>();
    }
}