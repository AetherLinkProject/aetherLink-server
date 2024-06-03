using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Scheduler;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace AetherLink.Worker.Core;

[DependsOn(
    typeof(AbpAutoMapperModule)
)]
public class AetherLinkServerWorkerCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<AetherLinkServerWorkerCoreModule>(); });
        context.Services.AddTransient<IStorageProvider, StorageProvider>();
        context.Services.AddTransient<IPriceDataProvider, PriceDataProvider>();
        context.Services.AddTransient<IJobProvider, JobProvider>();

        context.Services.AddTransient<IObservationCollectSchedulerJob, ObservationCollectSchedulerJob>();
        context.Services.AddTransient<IResetRequestSchedulerJob, ResetRequestSchedulerJob>();
        context.Services.AddSingleton<ISchedulerService, SchedulerService>();
    }
}