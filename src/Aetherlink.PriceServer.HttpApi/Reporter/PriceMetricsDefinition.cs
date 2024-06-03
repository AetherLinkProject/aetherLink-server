namespace AetherlinkPriceServer.Reporter;

public class PriceCollectMetricsDefinition
{
    public const string PriceCollectGaugeName = "price_collect";
    public static readonly string[] PriceCollectLabels = { "source", "token_pair" };
}

public class PriceQueryMetricsDefinition
{
    public const string PriceQueryCounterName = "price_query";
    public static readonly string[] PriceQueryLabels = { "app_id", "method" };
}