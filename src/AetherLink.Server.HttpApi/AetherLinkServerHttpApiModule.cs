using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace AetherLink.Server.HttpApi;

[DependsOn(
    typeof(AbpAspNetCoreMvcModule)
)]
public class AetherLinkServerHttpApiModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<AetherLinkServerHttpApiModule>(); });
        context.Services.AddSingleton<IChainBalanceProvider, EthBalanceProvider>();
        context.Services.AddSingleton<IChainBalanceProvider, SepoliaBalanceProvider>();
        context.Services.AddSingleton<IChainBalanceProvider, BscBalanceProvider>();
        context.Services.AddSingleton<IChainBalanceProvider, BscTestBalanceProvider>();
        context.Services.AddSingleton<IChainBalanceProvider, BaseBalanceProvider>();
        context.Services.AddSingleton<IChainBalanceProvider, BaseSepoliaBalanceProvider>();
        context.Services.AddSingleton<IChainBalanceProvider, TonBalanceProvider>();
    }
}