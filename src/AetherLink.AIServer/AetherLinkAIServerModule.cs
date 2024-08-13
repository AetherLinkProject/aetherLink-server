using AElf.Client.Service;
using AetherLink.AIServer.Core;
using AetherLink.AIServer.Core.ContractHandler;
using AetherLink.AIServer.Core.Options;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc.UI.MultiTenancy;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.AutoMapper;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.Modularity;

namespace AetherLink.AIServer;

[DependsOn(
    typeof(AetherLinkAIServerCoreModule),
    typeof(AbpAspNetCoreMvcUiMultiTenancyModule),
    typeof(AbpCachingStackExchangeRedisModule),
    typeof(AbpBackgroundWorkersModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpAutoMapperModule),
    typeof(AbpAutofacModule)
)]
public class AetherLinkAIServerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        ConfigureOptions(configuration);
        ConfigureGraphQl(context, configuration);
        context.Services.AddSingleton<IBlockchainClientFactory<AElfClient>, AElfClientFactory>();
        context.Services.AddHttpClient();
    }

    private void ConfigureOptions(IConfiguration configuration)
    {
        Configure<OpenAIOption>(configuration.GetSection("OpenAI"));
        Configure<SearcherOption>(configuration.GetSection("Searcher"));
        Configure<EnclaveOption>(configuration.GetSection("Enclave"));
        Configure<ContractOptions>(configuration.GetSection("Contract"));
        Configure<TransmitterOption>(configuration.GetSection("Transmitter"));

        // Configure<NetworkOptions>(configuration.GetSection("Network"));
        // Configure<HangfireOption>(configuration.GetSection("Hangfire"));
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