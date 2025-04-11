namespace AetherLink.Worker.Core.Options;

public class TonGetBlockProviderOptions
{
    public string Url { get; set; }
    public int Weight { get; set; } = 40;
    public string ApiKey { get; set; }
    public int ApiKeyPerSecondRequestLimit { get; set; } = 60;
    public int ApiKeyPerDayRequestLimit { get; set; } = 40000;
    public int ApiKeyPerMonthRequestLimit { get; set; } = 0;
}

public class TonCenterProviderApiConfig
{
    public string Url { get; set; }
    public int Weight { get; set; } = 60;
    public string ApiKey { get; set; }
    public int ApiKeyPerSecondRequestLimit { get; set; } = 10;
    public int NoApiKeyPerSecondRequestLimit { get; set; } = 1;
    public int TransactionsSubscribeDelay { get; set; } = 50;
}

public class TonapiProviderApiConfig
{
    public string Url { get; set; }
    public int Weight { get; set; } = 10;
    public string ApiKey { get; set; }
    public int ApiKeyPerSecondRequestLimit { get; set; } = 10;
    public int NoApiKeyPerSecondRequestLimit { get; set; } = 1;
}

public class ChainStackApiConfig
{
    public string Url { get; set; }
    public int Weight { get; set; } = 80;
    public string ApiKey { get; set; }
    public int ApiKeyPerSecondRequestLimit { get; set; } = 25;
    public int ApiKeyPerMonthRequestLimit { get; set; } = 3000000;
}