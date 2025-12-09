using System;
using Aetherlink.PriceServer.Dtos;

namespace AetherlinkPriceServer.Common;

public static class PriceConvertHelper
{
    public static long ConvertPrice(double price) =>
        Convert.ToInt64(Math.Round(price * Math.Pow(10, SymbolPriceConstants.DefaultDecimal)));

    public static long ConvertPrice(decimal price) =>
        Convert.ToInt64(Math.Round(price * (decimal)Math.Pow(10, SymbolPriceConstants.DefaultDecimal)));
}