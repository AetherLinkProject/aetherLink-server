using Aetherlink.PriceServer.Common;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace Aetherlink.PriceServer;

public class AetherlinkPriceServerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClient();
        context.Services.AddScoped<IHttpService, HttpService>();
        context.Services.AddTransient<IPriceServerProvider, PriceServerProvider>();
    }
}