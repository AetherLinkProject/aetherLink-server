using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Provider;
using Moq;

namespace Aetherlink.PriceServer.Tests;

public partial class AetherlinkPriceServerServiceTest
{
    private IPriceProvider GetMockIPriceProvider()
    {
        var priceProvider = new Mock<IPriceProvider>();

        priceProvider
            .Setup(p => p.GetPriceAsync(ELFUSDT,
                It.Is<SourceType>(s => s == SourceType.CoinGecko || s == SourceType.None))).ReturnsAsync(new PriceDto
            {
                TokenPair = ELFUSDT,
                Price = ELFPRICE,
                Decimal = SymbolPriceConstants.DefaultDecimal,
                UpdateTime = DateTime.Now
            });

        priceProvider.Setup(p => p.GetPriceListAsync(SourceType.CoinGecko, It.IsAny<List<string>>())).ReturnsAsync(
            new List<PriceDto>
            {
                new()
                {
                    TokenPair = ELFUSDT,
                    Price = ELFPRICE,
                    Decimal = SymbolPriceConstants.DefaultDecimal,
                    UpdateTime = DateTime.Now
                },
                new()
                {
                    TokenPair = BTCUSDT,
                    Price = BTCPRICE,
                    Decimal = SymbolPriceConstants.DefaultDecimal,
                    UpdateTime = DateTime.Now
                }
            });

        priceProvider.Setup(p => p.GetAllSourcePricesAsync(It.IsAny<string>())).ReturnsAsync(
            new List<PriceDto>
            {
                new()
                {
                    TokenPair = ELFUSDT,
                    Price = ELFPRICE,
                    Decimal = SymbolPriceConstants.DefaultDecimal,
                    UpdateTime = DateTime.Now
                },
                new()
                {
                    TokenPair = ELFUSDT,
                    Price = ELFPRICE + 100,
                    Decimal = SymbolPriceConstants.DefaultDecimal,
                    UpdateTime = DateTime.Now
                },
                new()
                {
                    TokenPair = ELFUSDT,
                    Price = ELFPRICE + 200,
                    Decimal = SymbolPriceConstants.DefaultDecimal,
                    UpdateTime = DateTime.Now
                },
                new()
                {
                    TokenPair = ELFUSDT,
                    Price = ELFPRICE + 300,
                    Decimal = SymbolPriceConstants.DefaultDecimal,
                    UpdateTime = DateTime.Now
                }
            });
        return priceProvider.Object;
    }
}