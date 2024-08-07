using AetherLink.AIServer.Core;
using AetherLink.AIServer.Core.Options;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc.UI.MultiTenancy;
using Volo.Abp.Autofac;
using Volo.Abp.AutoMapper;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.Modularity;

namespace AetherLink.AIServer;

[DependsOn(
    typeof(AetherLinkAIServerCoreModule),
    typeof(AbpAspNetCoreMvcUiMultiTenancyModule),
    typeof(AbpCachingStackExchangeRedisModule),
    typeof(AbpBackgroundWorkersModule),
    typeof(AbpAutoMapperModule),
    typeof(AbpAutofacModule)
)]
public class AetherLinkAIServerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        ConfigureOptions(configuration);

        // gRPC server
        context.Services.AddHostedService<AetherLinkAIServerHostedService>();

        // Singleton
        // Http Client and Service
        context.Services.AddHttpClient();
        // context.Services.AddScoped<IHttpClientService, HttpClientService>();

        ConfigureGraphQl(context, configuration);
    }

    private void ConfigureOptions(IConfiguration configuration)
    {
        Configure<SearcherOption>(configuration.GetSection("Searcher"));
        // Configure<ContractOptions>(configuration.GetSection("Chains"));
        // Configure<NetworkOptions>(configuration.GetSection("Network"));
        // Configure<HangfireOptions>(configuration.GetSection("Hangfire"));
        // Configure<SchedulerOptions>(configuration.GetSection("Scheduler"));
        // Configure<PriceFeedsOptions>(configuration.GetSection("PriceFeeds"));
        // Configure<ProcessJobOptions>(configuration.GetSection("ProcessJob"));
        // Configure<OracleInfoOptions>(configuration.GetSection("OracleChainInfo"));
        // Configure<AbpDistributedCacheOptions>(options => { options.KeyPrefix = "AetherLinkServer:"; });
    }


    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        context.AddBackgroundWorkerAsync<Searcher>();
        // context.AddBackgroundWorkerAsync<LogPoller>();
    }

    private void ConfigureGraphQl(ServiceConfigurationContext context, IConfiguration configuration)
    {
        Configure<GraphqlOption>(configuration.GetSection("GraphQL"));
        context.Services.AddSingleton(new GraphQLHttpClient(configuration["GraphQL:Configuration"],
            new NewtonsoftJsonSerializer()));
        context.Services.AddScoped<IGraphQLClient>(sp => sp.GetRequiredService<GraphQLHttpClient>());
    }
}