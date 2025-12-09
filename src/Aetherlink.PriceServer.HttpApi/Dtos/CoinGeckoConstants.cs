using System.Collections.Generic;

namespace AetherlinkPriceServer.Dtos;

public static class CoinGeckoConstants
{
    public static readonly Dictionary<string, string> IdMap = new()
    {
        { "ELF", "aelf" },
        { "BTC", "bitcoin" },
        { "ETH", "ethereum" },
        { "OP", "optimism" },
        { "TRX", "tron" },
        { "SOL", "solana" },
        { "ARB", "arbitrum" },
        { "USDT", "tether" },
        { "USDC", "usd-coin" },
        { "DAI", "dai" },
        { "MATIC", "matic-network" },
        { "BNB", "binancecoin" }
    };
}