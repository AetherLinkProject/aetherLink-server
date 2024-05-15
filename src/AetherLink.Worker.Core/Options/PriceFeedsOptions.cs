using System.Collections.Generic;

namespace AetherLink.Worker.Core.Options;

public class PriceFeedsOptions
{
    public string Source { get; set; }
    public int Timeout { get; set; } = 10;
    public CoinGeckoOptions CoinGecko { get; set; }
    public CoinBaseOptions CoinBase { get; set; }
    public CoinMarketOptions CoinMarket { get; set; }
    public OkexOptions Okex { get; set; }
}

public class CoinMarketOptions
{
    public string BaseUrl { get; set; }
    public string ApiKey { get; set; }
}

public class CoinGeckoOptions
{
    public Dictionary<string, string> CoinIdMapping { get; set; }
    public string BaseUrl { get; set; }
    public string ApiKey { get; set; }
}

public class CoinBaseOptions
{
    public string BaseUrl { get; set; }
}

public class OkexOptions
{
    public string ApiKey { get; set; }
    public string SecretKey { get; set; }
    public string Passphrase { get; set; }
}