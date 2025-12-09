namespace AetherLink.Worker.Core.Reporter.MetricsDefinition;

public class DataFeedsMetricsDefinition
{
    public const string DataFeedsGaugeName = "datafeeds";
    public const string JobCounterName = "datafeeds_sum";
    public static readonly string[] JobCounterLabels = { "chain_id", "request_id", "epoch", "round_id" };
}

public class PriceFeedsMetricsDefinition
{
    public const string PriceFeedsGaugeName = "pricefeeds";
    public static readonly string[] PriceLabels = { "currency_pair" };
}