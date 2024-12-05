using AElf.ExceptionHandler.ABP;
using AElf.Indexing.Elasticsearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.AuditLogging;
using Volo.Abp.AutoMapper;
using Volo.Abp.Domain.Entities.Events.Distributed;
using Volo.Abp.Emailing;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace AetherLinkServer
{
    [DependsOn(
        typeof(AetherLinkServerDomainSharedModule),
        typeof(AbpAuditLoggingDomainModule),
        typeof(AbpTenantManagementDomainModule),
        typeof(AbpEmailingModule),
        typeof(AElfIndexingElasticsearchModule),
        typeof(AOPExceptionModule)
    )]
    public class AetherLinkServerDomainModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<AbpMultiTenancyOptions>(options => { options.IsEnabled = false; });

#if DEBUG
            context.Services.Replace(ServiceDescriptor.Singleton<IEmailSender, NullEmailSender>());
#endif
            Configure<AbpAutoMapperOptions>(options => { options.AddMaps<AetherLinkServerDomainModule>(); });

            Configure<AbpDistributedEntityEventOptions>(options => { });
        }
    }
}