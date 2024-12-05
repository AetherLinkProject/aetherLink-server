using AetherLinkServer.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Modularity;

namespace AetherLinkServer.DbMigrator
{
    [DependsOn(
        typeof(AbpAutofacModule),
        typeof(AetherLinkServerEntityFrameworkCoreModule)
    )]
    public class AetherLinkServerDbMigratorModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<AbpBackgroundJobOptions>(options => options.IsJobExecutionEnabled = false);
        }
    }
}