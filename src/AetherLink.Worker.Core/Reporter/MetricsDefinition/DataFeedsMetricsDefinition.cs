namespace AetherLink.Worker.Core.Reporter.MetricsDefinition;

public class DataFeedsMetricsDefinition
{
    public const string JobCounterName = "datafeeds_sum";
    public static readonly string[] JobLabels = { "chain_id", "request_id", "epoch", "round_id" };
    public static string ExecuteTimeGaugeName = "datafeeds_execute";
}

public class PriceFeedsMetricsDefinition
{
    public const string PriceFeedsGaugeName = "pricefeeds";
    public static readonly string[] PriceLabels = { "currency_pair" };
}