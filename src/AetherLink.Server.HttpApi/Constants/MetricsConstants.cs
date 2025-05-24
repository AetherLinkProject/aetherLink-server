namespace AetherLink.Server.HttpApi.Constants;

public static class MetricsConstants
{
    public const string BalanceGaugeName = "chain_balance_gauge";
    public static readonly string[] BalanceGaugeLabels = { "chain", "address" };
    public const int MaxRetries = 5;
    public const int RetryDelayMs = 2000;
    public const int AddressDelayMs = 1000;
} 