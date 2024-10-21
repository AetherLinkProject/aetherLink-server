using AElf.ExceptionHandler;
using AetherLink.Metric;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Common.TonIndexer;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Reporter;
using AetherLink.Worker.Core.Scheduler;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace AetherLink.Worker.Core;

[DependsOn(
    typeof(AbpAutoMapperModule),
    typeof(AetherLinkMetricModule),
    typeof(AOPExceptionModule)
)]
public class AetherLinkServerWorkerCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<AetherLinkServerWorkerCoreModule>(); });
        context.Services.AddTransient<IStorageProvider, StorageProvider>();
        context.Services.AddTransient<IJobProvider, JobProvider>();

        context.Services.AddTransient<IObservationCollectSchedulerJob, ObservationCollectSchedulerJob>();
        context.Services.AddTransient<IResetRequestSchedulerJob, ResetRequestSchedulerJob>();
        context.Services.AddTransient<IResetCronUpkeepSchedulerJob, ResetCronUpkeepSchedulerJob>();
        context.Services.AddTransient<IResetLogTriggerSchedulerJob, ResetLogTriggerSchedulerJob>();
        context.Services.AddTransient<IPriceFeedsProvider, PriceFeedsProvider>();
        context.Services.AddSingleton<ISchedulerService, SchedulerService>();
        context.Services.AddTransient<IAeFinderProvider, AeFinderProvider>();
        // Reporter
        context.Services.AddSingleton<IWorkerReporter, WorkerReporter>();
        context.Services.AddSingleton<IVRFReporter, VRFReporter>();
        context.Services.AddSingleton<IDataFeedsReporter, DataFeedsReporter>();
        context.Services.AddSingleton<IReportReporter, ReportReporter>();
        context.Services.AddSingleton<IMultiSignatureReporter, MultiSignatureReporter>();
        
        // ton
        context.Services.AddSingleton<ITonIndexerProvider, TonCenterApi>();
        context.Services.AddSingleton<ITonIndexerProvider, GetBlockApi>();
        context.Services.AddSingleton<ITonIndexerProvider, TonapiApi>();
        context.Services.AddSingleton<ITonIndexerProvider, ChainStackApi>();
    }
}