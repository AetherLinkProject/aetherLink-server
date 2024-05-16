using System;
using AElf.Client.Service;
using CoinGecko.Clients;
using CoinGecko.Interfaces;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Hangfire;
using Hangfire.Redis.StackExchange;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AetherLink.Worker.Core;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Common.ContractHandler;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.PeerManager;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Service;
using AetherLink.Worker.Core.Worker;
using Hangfire.Dashboard;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc.UI.MultiTenancy;
using Volo.Abp.Autofac;
using Volo.Abp.AutoMapper;
using Volo.Abp.BackgroundJobs.Hangfire;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.Modularity;
using Volo.Abp.Threading;

namespace AetherLink.Worker
{
    [DependsOn(
        typeof(AetherLinkServerWorkerCoreModule),
        typeof(AbpAspNetCoreMvcUiMultiTenancyModule),
        typeof(AbpCachingStackExchangeRedisModule),
        typeof(AbpBackgroundJobsHangfireModule),
        typeof(AbpBackgroundWorkersModule),
        typeof(AbpAutoMapperModule),
        typeof(AbpAutofacModule)
    )]
    public class AetherLinkServerWorkerModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var configuration = context.Services.GetConfiguration();
            Configure<WorkerOptions>(configuration.GetSection("Worker"));
            Configure<ContractOptions>(configuration.GetSection("Chains"));
            Configure<NetworkOptions>(configuration.GetSection("Network"));
            Configure<HangfireOptions>(configuration.GetSection("Hangfire"));
            Configure<SchedulerOptions>(configuration.GetSection("Scheduler"));
            Configure<ProcessJobOptions>(configuration.GetSection("ProcessJob"));
            Configure<OracleInfoOptions>(configuration.GetSection("OracleChainInfo"));
            Configure<AbpDistributedCacheOptions>(options => { options.KeyPrefix = "AetherLinkServer:"; });
            context.Services.AddHostedService<AetherLinkServerHostedService>();
            context.Services.AddSingleton<IPeerManager, PeerManager>();
            context.Services.AddSingleton<IRetryProvider, RetryProvider>();
            context.Services.AddSingleton<IServer, Server>();
            context.Services.AddSingleton<IStateProvider, StateProvider>();
            context.Services.AddSingleton<IWorkerProvider, WorkerProvider>();
            context.Services.AddSingleton<IContractProvider, ContractProvider>();
            context.Services.AddSingleton<IRecurringJobManager, RecurringJobManager>();
            context.Services.AddSingleton<IOracleContractProvider, OracleContractProvider>();
            context.Services.AddSingleton<IBlockchainClientFactory<AElfClient>, AElfClientFactory>();
            context.Services.AddSingleton<AetherLinkServer.AetherLinkServerBase, AetherLinkService>();
            context.Services.AddHttpClient();
            context.Services.AddScoped<IHttpClientService, HttpClientService>();

            ConfigureGraphQl(context, configuration);
            ConfigureHangfire(context, configuration);
            ConfigureDataFeeds(context, configuration);
            ConfigureRequestJobs(context);
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            var configuration = context.GetConfiguration();
            var hangfireOptions = configuration.GetSection("Hangfire").Get<HangfireOptions>();
            if (hangfireOptions.UseDashboard)
            {
                var app = context.GetApplicationBuilder();

                var dashboardOptions = new DashboardOptions
                {
                    Authorization = new[] { new CustomAuthorizeFilter() }
                };
                app.UseHangfireDashboard("/hangfire", dashboardOptions);
            }

            context.AddBackgroundWorkerAsync<SearchWorker>();
            AsyncHelper.RunSync(
                async () => { await context.ServiceProvider.GetService<IServer>().StartAsync(); });
        }

        private void ConfigureHangfire(ServiceConfigurationContext context, IConfiguration configuration)
        {
            var hangfireOptions = configuration.GetSection("Hangfire").Get<HangfireOptions>();
            var options = new RedisStorageOptions
            {
                Prefix = hangfireOptions.RedisStorage.Prefix,
                Db = hangfireOptions.RedisStorage.DbIndex
            };

            context.Services.AddHangfire(config =>
            {
                config.UseRedisStorage(hangfireOptions.RedisStorage.Host, options);
                config.UseDashboardMetric(DashboardMetrics.ServerCount)
                    .UseDashboardMetric(DashboardMetrics.RecurringJobCount)
                    .UseDashboardMetric(DashboardMetrics.RetriesCount)
                    .UseDashboardMetric(DashboardMetrics.AwaitingCount)
                    .UseDashboardMetric(DashboardMetrics.EnqueuedAndQueueCount)
                    .UseDashboardMetric(DashboardMetrics.ScheduledCount)
                    .UseDashboardMetric(DashboardMetrics.ProcessingCount)
                    .UseDashboardMetric(DashboardMetrics.SucceededCount)
                    .UseDashboardMetric(DashboardMetrics.FailedCount)
                    .UseDashboardMetric(DashboardMetrics.EnqueuedCountOrNull)
                    .UseDashboardMetric(DashboardMetrics.FailedCountOrNull)
                    .UseDashboardMetric(DashboardMetrics.DeletedCount);
            });

            context.Services.AddHangfireServer();
        }

        private void ConfigureGraphQl(ServiceConfigurationContext context, IConfiguration configuration)
        {
            Configure<GraphqlOptions>(configuration.GetSection("GraphQL"));
            context.Services.AddSingleton(new GraphQLHttpClient(configuration["GraphQL:Configuration"],
                new NewtonsoftJsonSerializer()));
            context.Services.AddScoped<IGraphQLClient>(sp => sp.GetRequiredService<GraphQLHttpClient>());
        }

        private void ConfigureRequestJobs(ServiceConfigurationContext context)
        {
            context.Services.AddSingleton<IRequestJob, VrfRequestJobHandler>();
            context.Services.AddSingleton<IRequestJob, DataFeedRequestJobHandler>();
        }

        private void ConfigureDataFeeds(ServiceConfigurationContext context, IConfiguration configuration)
        {
            Configure<PriceFeedsOptions>(configuration.GetSection("PriceFeeds"));
            context.Services.AddSingleton<IPriceDataProvider, PriceDataProvider>();
            context.Services.AddSingleton<ICoinGeckoClient, CoinGeckoClient>();

            context.Services.AddHttpClient("CoinGeckoPro", client =>
            {
                client.BaseAddress = new Uri(configuration["PriceFeeds:CoinGecko:BaseUrl"]);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("x-cg-pro-api-key", configuration["PriceFeeds:CoinGecko:ApiKey"]);
            });
        }
    }
}