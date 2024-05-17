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
    typeof(AbpAspNetCoreMvcModule))]
public class AetherlinkPriceServerHttpApiModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<AetherlinkPriceServerHttpApiModule>(); });
        context.Services.AddTransient<IPriceProvider, PriceProvider>();
        context.Services.AddTransient<IStorageProvider, StorageProvider>();
        context.Services.AddSingleton<ICoinGeckoClient, CoinGeckoClient>();
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var backgroundWorkerManger = context.ServiceProvider.GetRequiredService<IBackgroundWorkerManager>();
        backgroundWorkerManger.AddAsync(context.ServiceProvider.GetService<CoinGeckoTokenPriceSearchWorker>());
        backgroundWorkerManger.AddAsync(context.ServiceProvider.GetService<OkxTokenPriceSearchWorker>());
        backgroundWorkerManger.AddAsync(context.ServiceProvider.GetService<BinancePriceSearchWorker>());
        backgroundWorkerManger.AddAsync(context.ServiceProvider.GetService<CoinMarketTokenPriceSearchWorker>());
        backgroundWorkerManger.AddAsync(context.ServiceProvider.GetService<GateIoPriceSearchWorker>());
        backgroundWorkerManger.AddAsync(context.ServiceProvider.GetService<CoinBaseTokenPriceSearchWorker>());
        backgroundWorkerManger.AddAsync(context.ServiceProvider.GetService<HourlyPriceWorker>());
    }
}