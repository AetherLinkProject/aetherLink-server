namespace AetherlinkPriceServer.Reporter;

public class PriceCollectMetricsDefinition
{
    public const string PriceCollectGaugeName = "price_collect";
    public static readonly string[] PriceCollectLabels = { "source", "token_pair" };
}

public class PriceQueryMetricsDefinition
{
    public const string PriceQueryGaugeName = "price_query";
}

// public class DataFeedsMetricsDefinition
// {
//     public const string DataFeedsGaugeName = "datafeeds";
//     public const string DataFeedsSumName = "datafeeds_sum";
//     public static readonly string[] DataFeedsJobSumLabels = { "chain_id", "request_id", "epoch", "round_id" };
// }