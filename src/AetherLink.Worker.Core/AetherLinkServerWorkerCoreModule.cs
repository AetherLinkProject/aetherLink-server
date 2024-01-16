using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Scheduler;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace AetherLink.Worker.Core
{
    [DependsOn(
        typeof(AbpAutoMapperModule)
    )]
    public class AetherLinkServerWorkerCoreModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<AbpAutoMapperOptions>(options => { options.AddMaps<AetherLinkServerWorkerCoreModule>(); });
            context.Services.AddTransient<IJobRequestProvider, JobRequestProvider>();
            context.Services.AddSingleton<ISchedulerService, SchedulerService>();
            context.Services.AddSingleton<ISchedulerJob, ResetRequestSchedulerJob>();
            context.Services.AddTransient<IPriceDataProvider, PriceDataProvider>();
        }
    }
}