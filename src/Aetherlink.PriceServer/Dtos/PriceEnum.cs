namespace Aetherlink.PriceServer.Dtos;

public enum AggregateType
{
    Latest,
    Avg,
    Medium
}

public enum SourceType
{
    CoinGecko,
    Okx,
    Binance,
    CoinMarket,
    GateIo,
    CoinBase,
    None = 99
}