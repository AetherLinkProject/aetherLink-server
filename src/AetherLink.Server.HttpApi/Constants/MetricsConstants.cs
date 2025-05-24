namespace AetherLink.Server.HttpApi.Constants;

public static class MetricsConstants
{
    public const string BalanceGaugeName = "chain_balance_gauge";
    public static readonly string[] BalanceGaugeLabels = { "chain", "address" };
} 