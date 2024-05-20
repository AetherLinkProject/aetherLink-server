using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Application;
using AetherlinkPriceServer.Controller;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Aetherlink.PriceServer.Tests;

[Collection(AetherlinkTestConsts.CollectionDefinitionName)]
public partial class AetherlinkPriceServerServiceTest : AetherlinkPriceServerTestBase
{
    private readonly PriceController _priceController;
    private readonly IPriceAppService _priceAppService;

    public AetherlinkPriceServerServiceTest()
    {
        _priceController = GetRequiredService<PriceController>();
        _priceAppService = GetRequiredService<IPriceAppService>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.AddSingleton(GetMockIPriceProvider());
    }

    [Fact]
    public async Task GetTokenPriceTest()
    {
        var source = SourceType.CoinGecko;
        var result = await _priceAppService.GetTokenPriceAsync(new()
        {
            TokenPair = ELFUSDT,
            Source = source
        });
        result.Source.ShouldBe(source.ToString());
        result.Data.TokenPair.ShouldBe(ELFUSDT);
        result.Data.Price.ShouldBe(ELFPRICE);
        result.Data.Decimal.ShouldBe(SymbolPriceConstants.DefaultDecimal);
    }

    [Fact]
    public async Task GetNotSupportedTokenPairTest()
    {
        var result = await _priceAppService.GetTokenPriceAsync(new()
        {
            TokenPair = "not-exist",
            Source = SourceType.CoinGecko
        });
        result.Data.ShouldBeNull();
    }

    [Fact]
    public async Task GetNotSupportedSourceTest()
    {
        var result = await _priceAppService.GetTokenPriceAsync(new()
        {
            TokenPair = ELFUSDT,
            Source = (SourceType)10
        });

        result.Data.ShouldBeNull();
    }

    [Fact]
    public async Task GetTokenPriceListTest()
    {
        var source = SourceType.CoinGecko;
        var result = await _priceAppService.GetTokenPriceListAsync(new()
        {
            TokenPairs = new() { ELFUSDT, BTCUSDT },
            Source = source
        });
        result.Source.ShouldBe(source.ToString());
        result.Prices.Count.ShouldBe(2);
        result.Prices.FirstOrDefault(t => t.TokenPair == ELFUSDT)?.Price.ShouldBe(ELFPRICE);
        result.Prices.FirstOrDefault(t => t.TokenPair == BTCUSDT)?.Price.ShouldBe(BTCPRICE);
        result.Prices.All(t => t.Decimal == SymbolPriceConstants.DefaultDecimal).ShouldBeTrue();
    }

    [Fact]
    public async Task GetNotExistTokenInListTest()
    {
        var result = await _priceAppService.GetTokenPriceListAsync(new()
        {
            TokenPairs = new() { ELFUSDT, BTCUSDT, "not-exist" },
            Source = SourceType.CoinGecko
        });
        result.Prices.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetTokenPriceListWithNotExistSourceTest()
    {
        var result = await _priceAppService.GetTokenPriceListAsync(new()
        {
            TokenPairs = new() { ELFUSDT, BTCUSDT },
            Source = (SourceType)10
        });
        result.Prices.ShouldBeNull();
    }

    [Fact]
    public async Task GetLatestTokenPriceTest()
    {
        var type = AggregateType.Latest;
        var result = await _priceAppService.GetAggregatedTokenPriceAsync(new()
        {
            TokenPair = ELFUSDT,
            AggregateType = type
        });

        result.AggregateType.ShouldBe(type.ToString());
        result.Data.Price.ShouldBe(ELFPRICE);
        result.Data.TokenPair.ShouldBe(ELFUSDT);
    }

    [Fact]
    public async Task GetAvgTokenPriceTest()
    {
        var type = AggregateType.Avg;
        var result = await _priceAppService.GetAggregatedTokenPriceAsync(new()
        {
            TokenPair = ELFUSDT,
            AggregateType = type
        });

        result.AggregateType.ShouldBe(type.ToString());
        // (0+100+200+300)/4
        result.Data.Price.ShouldBe(ELFPRICE + 150);
        result.Data.TokenPair.ShouldBe(ELFUSDT);
    }

    [Fact]
    public async Task GetMediumTokenPriceTest()
    {
        var type = AggregateType.Medium;
        var result = await _priceAppService.GetAggregatedTokenPriceAsync(new()
        {
            TokenPair = ELFUSDT,
            AggregateType = type
        });

        result.AggregateType.ShouldBe(type.ToString());
        // 0,100,200,300
        result.Data.Price.ShouldBe(ELFPRICE + 100);
        result.Data.TokenPair.ShouldBe(ELFUSDT);
    }

    [Fact]
    public async Task GetNotExistAggregatedTokenPriceTest()
    {
        var result = await _priceAppService.GetAggregatedTokenPriceAsync(new()
        {
            TokenPair = ELFUSDT,
            AggregateType = (AggregateType)10
        });
        result.Data.ShouldBeNull();
    }
}