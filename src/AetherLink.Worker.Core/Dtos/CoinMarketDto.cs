using System.Collections.Generic;

namespace AetherLink.Worker.Core.Dtos;

public class CoinMarketResponseDto
{
    public Dictionary<string, CoinMarketPriceDto> Data { get; set; }
}

public class CoinMarketPriceDto
{
    public Dictionary<string, CoinMarketPriceQuoteDto> Quote { get; set; }
}

public class CoinMarketPriceQuoteDto
{
    public double Price { get; set; }
}