namespace AetherlinkPriceServer.Reporter;

public class PriceCollectMetricsDefinition
{
    public const string PriceCollectGaugeName = "price_collect";
    public const string ThirdPartyCollectLatencyName = "third_party_collect_latency_seconds";
    public static readonly string[] PriceCollectLabels = { "source", "token_pair" };
    public static readonly string[] ThirdPartyCollectLabels = { "source", "token_pair" };
}

public class PriceQueryMetricsDefinition
{
    public const string PriceRequestLatencyName = "price_request_latency_seconds";
    public const string PriceQueryRequestsTotalName = "price_query_requests_total";
    public const string AggregatedPriceRequestsTotalName = "aggregated_price_requests_total";
    public const string PriceRequestsCacheHitsTotalName = "price_query_requests_cache_hits_total";

    public static readonly string[] PriceRequestLatencyLabels = { "app_id", "router" };
    public static readonly string[] PriceQueryRequestsTotalLabels = { "app_id", "router" };
    public static readonly string[] PriceRequestsCacheHitsTotalLabels = { "app_id", "token_pair" };
    public static readonly string[] AggregatedPriceRequestsTotalLabels = { "app_id", "token_pair", "aggregated_type" };
}