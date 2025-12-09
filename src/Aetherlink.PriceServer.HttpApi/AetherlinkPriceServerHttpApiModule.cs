using AetherLink.Metric;
using AetherlinkPriceServer.Provider;
using CoinGecko.Clients;
using CoinGecko.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AutoMapper;
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
        context.Services.AddTransient<IOkxProvider, OkxProvider>();
        context.Services.AddTransient<IPriceProvider, PriceProvider>();
        context.Services.AddTransient<IGateIoProvider, GateIoProvider>();
        context.Services.AddTransient<IStorageProvider, StorageProvider>();
        context.Services.AddTransient<IBinanceProvider, BinanceProvider>();
        context.Services.AddTransient<ICoinBaseProvider, CoinBaseProvider>();
        context.Services.AddTransient<ICoinGeckoProvider, CoinGeckoProvider>();
        context.Services.AddTransient<ICoinMarketProvider, CoinMarketProvider>();
        context.Services.AddTransient<IHistoricPriceProvider, HistoricPriceProvider>();
        context.Services.AddSingleton<ICoinGeckoClient, CoinGeckoClient>();
    }
}