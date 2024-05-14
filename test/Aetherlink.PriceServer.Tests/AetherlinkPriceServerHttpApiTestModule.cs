using AetherlinkPriceServer;
using AetherlinkPriceServer.Options;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace Aetherlink.PriceServer.Tests;

[DependsOn(
    typeof(AetherlinkPriceServerHttpApiModule),
    typeof(AetherlinkPriceServerModule)
)]
public class AetherlinkPriceServerHttpApiTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.Configure<TokenPriceSourceOptions>(option =>
        {
            option.Sources = new Dictionary<string, TokenPriceSourceOption>
            {
                {
                    "CoinGecko", new()
                    {
                        Name = "CoinGecko",
                        Interval = 3000,
                        Tokens = new() { "aelf,ELF" }
                    }
                },
                {
                    "Okx", new()
                    {
                        Name = "Okx",
                        Interval = 3000,
                        Tokens = new() { "ELF-USDT" }
                    }
                },
                {
                    "Binance", new()
                    {
                        Name = "Binance",
                        Interval = 3000,
                        Tokens = new() { "ELF-USDT" }
                    }
                },
                {
                    "CoinMarket", new()
                    {
                        Name = "CoinMarket",
                        BaseUrl = "https://",
                        ApiKey = "123",
                        Interval = 3000,
                        Tokens = new() { "ELF-USDT" }
                    }
                },
                {
                    "GateIo", new()
                    {
                        Name = "GateIo",
                        Interval = 3000,
                        Tokens = new() { "ELF-USDT" }
                    }
                },
                {
                    "CoinBase", new()
                    {
                        Name = "CoinBase",
                        BaseUrl = "https://",
                        Interval = 3000,
                        Tokens = new() { "ELF-USDT" }
                    }
                }
            };
        });
        base.ConfigureServices(context);
    }
}