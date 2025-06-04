namespace AetherLink.Server.HttpApi.Constants;

public static class MetricsConstants
{
    public const string BalanceGaugeName = "chain_balance_gauge";
    public static readonly string[] BalanceGaugeLabels = { "chain", "address" };
    public const int MaxRetries = 5;
    public const int RetryDelayMs = 2000;
    public const int AddressDelayMs = 1000;
    public const string ChainTon = "ton";
    public const string ChainEvm = "evm";
}

public static class RequestTypeConst
{
    public const int Datafeeds = 1;
    public const int Vrf = 2;
    public const int Automation = 3;
}

public static class StartedRequestTypeName
{
    public const string Datafeeds = "datafeeds";
    public const string Vrf = "vrf";
    public const string Automation = "automation";
    public const string Crosschain = "crosschain";
} 