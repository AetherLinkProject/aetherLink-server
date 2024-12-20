﻿using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AuditLogging.MongoDB;
using Volo.Abp.FeatureManagement.MongoDB;
using Volo.Abp.Identity.MongoDB;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict.MongoDB;
using Volo.Abp.PermissionManagement.MongoDB;
using Volo.Abp.SettingManagement.MongoDB;
using Volo.Abp.TenantManagement.MongoDB;
using Volo.Abp.Uow;

namespace AetherLinkServer.EntityFrameworkCore
{
    [DependsOn(
        typeof(AetherLinkServerDomainModule),
        typeof(AbpPermissionManagementMongoDbModule),
        typeof(AbpSettingManagementMongoDbModule),
        typeof(AbpIdentityMongoDbModule),
        typeof(AbpOpenIddictMongoDbModule),
        typeof(AbpAuditLoggingMongoDbModule),
        typeof(AbpTenantManagementMongoDbModule),
        typeof(AbpFeatureManagementMongoDbModule)
    )]
    public class AetherLinkServerEntityFrameworkCoreModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddMongoDbContext<AetherLinkServerDbContext>(options => { options.AddDefaultRepositories(); });

            Configure<AbpUnitOfWorkDefaultOptions>(options =>
            {
                options.TransactionBehavior = UnitOfWorkTransactionBehavior.Disabled;
            });
        }
    }
}