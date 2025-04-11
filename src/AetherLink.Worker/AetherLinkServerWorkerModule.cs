using AElf.Client.Service;
using AetherLink.Metric;
using Aetherlink.PriceServer;
using Hangfire;
using Hangfire.Redis.StackExchange;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AetherLink.Worker.Core;
using AetherLink.Worker.Core.ChainHandler;
using AetherLink.Worker.Core.ChainKeyring;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Common.ContractHandler;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.PeerManager;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Provider.SearcherProvider;
using AetherLink.Worker.Core.Service;
using AetherLink.Worker.Core.Worker;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Builder;
using Prometheus;
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
        typeof(AetherlinkPriceServerModule),
        typeof(AbpBackgroundWorkersModule),
        typeof(AbpAutoMapperModule),
        typeof(AbpAutofacModule)
    )]
    public class AetherLinkServerWorkerModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var configuration = context.Services.GetConfiguration();
            ConfigureOptions(configuration);

            // gRPC server
            context.Services.AddHostedService<AetherLinkServerHostedService>();

            // Singleton
            context.Services.AddSingleton<IServer, Server>();
            context.Services.AddSingleton<IPeerManager, PeerManager>();
            context.Services.AddSingleton<IRetryProvider, RetryProvider>();
            context.Services.AddSingleton<IStateProvider, StateProvider>();
            context.Services.AddSingleton<IWorkerProvider, WorkerProvider>();
            context.Services.AddSingleton<IEvmSearchServer, EvmSearchServer>();
            context.Services.AddSingleton<IContractProvider, ContractProvider>();
            context.Services.AddSingleton<ITonStorageProvider, TonStorageProvider>();
            context.Services.AddSingleton<IRecurringJobManager, RecurringJobManager>();
            context.Services.AddSingleton<IOracleContractProvider, OracleContractProvider>();
            context.Services.AddSingleton<IBlockchainClientFactory<AElfClient>, AElfClientFactory>();
            context.Services.AddSingleton<AetherLinkServer.AetherLinkServerBase, AetherLinkService>();

            // Http Client and Service
            context.Services.AddHttpClient();
            context.Services.AddScoped<IHttpClientService, HttpClientService>();

            ConfigureHangfire(context, configuration);
            ConfigureMetrics(context, configuration);
            ConfigureRequestJobs(context);
            ConfigureChainKeyring(context);
            ConfigureChainHandler(context);
            ConfigureEventFilter(context);
        }

        private void ConfigureOptions(IConfiguration configuration)
        {
            Configure<WorkerOptions>(configuration.GetSection("Worker"));
            Configure<EvmOptions>(configuration.GetSection("EvmOptions"));
            Configure<ContractOptions>(configuration.GetSection("Chains"));
            Configure<NetworkOptions>(configuration.GetSection("Network"));
            Configure<HangfireOptions>(configuration.GetSection("Hangfire"));
            Configure<SchedulerOptions>(configuration.GetSection("Scheduler"));
            Configure<PriceFeedsOptions>(configuration.GetSection("PriceFeeds"));
            Configure<ProcessJobOptions>(configuration.GetSection("ProcessJob"));
            Configure<EvmContractsOptions>(configuration.GetSection("EvmContracts"));
            Configure<OracleInfoOptions>(configuration.GetSection("OracleChainInfo"));
            Configure<TonChainStatesOptions>(configuration.GetSection("TonChainStates"));
            Configure<TargetContractOptions>(configuration.GetSection("TargetContract"));
            Configure<TonPublicOptions>(configuration.GetSection("Chains:ChainInfos:Ton"));
            Configure<TonPrivateOptions>(configuration.GetSection("OracleChainInfo:ChainConfig:Ton"));
            Configure<AbpDistributedCacheOptions>(options => { options.KeyPrefix = "AetherLinkServer:"; });
            Configure<ChainStackApiConfig>(configuration.GetSection("Chains:ChainInfos:Ton:Indexer:ChainStack"));
            Configure<TonapiProviderApiConfig>(configuration.GetSection("Chains:ChainInfos:Ton:Indexer:TonApi"));
            Configure<TonGetBlockProviderOptions>(configuration.GetSection("Chains:ChainInfos:Ton:Indexer:GetBlock"));
            Configure<TonCenterProviderApiConfig>(configuration.GetSection("Chains:ChainInfos:Ton:Indexer:TonCenter"));
        }

        private void ConfigureMetrics(ServiceConfigurationContext context, IConfiguration configuration)
        {
            var metricsOption = configuration.GetSection("Metrics").Get<MetricsOption>();
            context.Services.AddMetricServer(options => { options.Port = metricsOption.Port; });
            context.Services.AddHealthChecks();
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            var app = context.GetApplicationBuilder();
            app.UseRouting();
            app.UseGrpcMetrics();
            app.UseEndpoints(endpoints => { endpoints.MapMetrics(); });

            var dashboardOptions = new DashboardOptions { Authorization = new[] { new CustomAuthorizeFilter() } };
            app.UseHangfireDashboard("/hangfire", dashboardOptions);

            ConfigureBackgroundWorker(context);
            AsyncHelper.RunSync(async () => { await context.ServiceProvider.GetService<IServer>().StartAsync(); });
            AsyncHelper.RunSync(async () =>
            {
                await context.ServiceProvider.GetService<IEvmSearchServer>().StartAsync();
            });
        }

        private void ConfigureBackgroundWorker(ApplicationInitializationContext context)
        {
            context.AddBackgroundWorkerAsync<LogsPoller>();
            context.AddBackgroundWorkerAsync<SearchWorker>();
            context.AddBackgroundWorkerAsync<TonIndexerWorker>();
            context.AddBackgroundWorkerAsync<TonChainStatesWorker>();
            context.AddBackgroundWorkerAsync<UnconfirmedWorker>();
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

        private void ConfigureRequestJobs(ServiceConfigurationContext context)
        {
            context.Services.AddSingleton<IRequestJob, VrfRequestJobHandler>();
            context.Services.AddSingleton<IRequestJob, DataFeedRequestJobHandler>();
            context.Services.AddSingleton<IRequestJob, AutomationRequestJobHandler>();
        }

        private void ConfigureChainKeyring(ServiceConfigurationContext context)
        {
            context.Services.AddSingleton<IChainKeyring, AElfChainKeyring>();
            context.Services.AddSingleton<IChainKeyring, TDVVChainKeyring>();
            context.Services.AddSingleton<IChainKeyring, TDVWChainKeyring>();
            context.Services.AddSingleton<IChainKeyring, TonChainKeyring>();
            context.Services.AddSingleton<IChainKeyring, EvmChainKeyring>();
            context.Services.AddSingleton<IChainKeyring, BscChainKeyring>();
            context.Services.AddSingleton<IChainKeyring, SEPOLIAChainKeyring>();
            context.Services.AddSingleton<IChainKeyring, BaseSepoliaChainKeyring>();
            context.Services.AddSingleton<IChainKeyring, BscTestChainKeyring>();
        }

        private void ConfigureChainHandler(ServiceConfigurationContext context)
        {
            // writer
            context.Services.AddSingleton<IChainWriter, AElfChainWriter>();
            context.Services.AddSingleton<IChainWriter, TDVVChainWriter>();
            context.Services.AddSingleton<IChainWriter, TDVWChainWriter>();
            context.Services.AddSingleton<IChainWriter, TonChainWriter>();
            context.Services.AddSingleton<IChainWriter, BscTestChainWriter>();
            context.Services.AddSingleton<IChainWriter, SEPOLIAChainWriter>();
            context.Services.AddSingleton<IChainWriter, BaseSepoliaWriter>();
            context.Services.AddSingleton<IChainWriter, EvmChainWriter>();
            context.Services.AddSingleton<IChainWriter, BscChainWriter>();

            // reader
            context.Services.AddSingleton<IChainReader, AElfChainReader>();
            context.Services.AddSingleton<IChainReader, TDVVChainReader>();
            context.Services.AddSingleton<IChainReader, TDVWChainReader>();
            context.Services.AddSingleton<IChainReader, TonChainReader>();
            context.Services.AddSingleton<IChainReader, BscChainReader>();
            context.Services.AddSingleton<IChainReader, EvmChainReader>();
            context.Services.AddSingleton<IChainReader, SEPOLIAChainReader>();
            context.Services.AddSingleton<IChainReader, BaseSepoliaChainReader>();
            context.Services.AddSingleton<IChainReader, BscTestChainReader>();
        }

        private void ConfigureEventFilter(ServiceConfigurationContext context)
        {
            context.Services.AddSingleton<IEventFilter, AutomationFilter>();
        }
    }
}