using System;
using AetherLink.Metric;
using AetherlinkPriceServer.Provider;
using AetherlinkPriceServer.Worker;
using CoinGecko.Clients;
using CoinGecko.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AutoMapper;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Modularity;

namespace AetherlinkPriceServer;

[DependsOn(
    typeof(AbpAspNetCoreMvcModule),
    typeof(AetherLinkMetricModule)
)]
public class AetherlinkPriceServerHttpApiModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<AetherlinkPriceServerHttpApiModule>(); });
        context.Services.AddTransient<IPriceProvider, PriceProvider>();
        context.Services.AddTransient<IStorageProvider, StorageProvider>();
        context.Services.AddTransient<ICoinBaseProvider, CoinBaseProvider>();
        context.Services.AddTransient<ICoinGeckoProvider, CoinGeckoProvider>();
        context.Services.AddTransient<IHistoricPriceProvider, HistoricPriceProvider>();
        context.Services.AddSingleton<ICoinGeckoClient, CoinGeckoClient>();

        ConfigCoinGeckoApi(context);
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var backgroundWorkerManger = context.ServiceProvider.GetRequiredService<IBackgroundWorkerManager>();
        // backgroundWorkerManger.AddAsync(context.ServiceProvider.GetService<CoinGeckoTokenPriceSearchWorker>());
        // backgroundWorkerManger.AddAsync(context.ServiceProvider.GetService<OkxTokenPriceSearchWorker>());
        // backgroundWorkerManger.AddAsync(context.ServiceProvider.GetService<BinancePriceSearchWorker>());
        // backgroundWorkerManger.AddAsync(context.ServiceProvider.GetService<CoinMarketTokenPriceSearchWorker>());
        // backgroundWorkerManger.AddAsync(context.ServiceProvider.GetService<GateIoPriceSearchWorker>());
        backgroundWorkerManger.AddAsync(context.ServiceProvider.GetService<CoinBaseTokenPriceSearchWorker>());
        // backgroundWorkerManger.AddAsync(context.ServiceProvider.GetService<HourlyPriceWorker>());
    }

    private void ConfigCoinGeckoApi(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        context.Services.AddHttpClient("CoinGeckoPro", client =>
        {
            client.BaseAddress = new Uri(configuration["TokenPriceSource:Sources:CoinGecko:BaseUrl"]);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("x-cg-pro-api-key",
                configuration["TokenPriceSource:Sources:CoinGecko:ApiKey"]);
        });
    }
}