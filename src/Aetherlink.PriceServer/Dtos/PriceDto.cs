using System;

namespace Aetherlink.PriceServer.Dtos;

public class PriceDto
{
    public string TokenPair { get; set; }
    public long Price { get; set; }
    public decimal Decimal { get; set; } = SymbolPriceConstants.DefaultDecimal;
    public DateTime UpdateTime { get; set; }
}