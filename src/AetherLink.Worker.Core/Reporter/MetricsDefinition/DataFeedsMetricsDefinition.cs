namespace AetherLink.Worker.Core.Reporter.MetricsDefinition;

public class DataFeedsMetricsDefinition
{
    public const string DataFeedsGaugeName = "datafeeds";
    public static readonly string[] PriceLabels = { "currency_pair" };
    public static readonly string[] DataFeedsJobLabels = { "chain_id", "request_id", "epoch", "round_id" };
}