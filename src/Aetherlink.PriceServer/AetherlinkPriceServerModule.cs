using Aetherlink.PriceServer.Common;
using Aetherlink.PriceServer.Options;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace Aetherlink.PriceServer;

public class AetherlinkPriceServerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        Configure<TokenPriceOption>(configuration.GetSection("TokenPrice"));
        context.Services.AddHttpClient();
        context.Services.AddScoped<IHttpService, HttpService>();
        context.Services.AddTransient<IPriceServerProvider, PriceServerProvider>();
    }
}