namespace AetherLink.Server.HttpApi.Constants;

public static class MetricsConstants
{
    // ===================== Gauge =====================
    public const string BalanceGaugeName = "chain_balance_gauge";
    public static readonly string[] BalanceGaugeLabels = { "chain", "address" };

    // ===================== Counter =====================
    public const string CrossChainQueryHitCounter = "cross_chain_query_hit_count";
    public static readonly string[] CrossChainQueryHitCounterLabels = { "id", "chain", "hit" };
    public const string CrossChainQueryHitCounterHelp = "Number of cross-chain query hits (by id, chain, hit)";

    public const string CrossChainQueryTotalCounter = "cross_chain_query_total_count";
    public static readonly string[] CrossChainQueryTotalCounterLabels = { "id" };
    public const string CrossChainQueryTotalCounterHelp = "Total number of cross-chain queries (by id)";

    public const string StartedRequestCounter = "started_request";
    public static readonly string[] StartedRequestCounterLabels = { "id", "source_chain", "target_chain", "type" };

    public const string StartedRequestCounterHelp =
        "Number of started business tasks (by id, source_chain, target_chain & type)";

    public const string CommittedReportCounter = "committed_report";
    public static readonly string[] CommittedReportCounterLabels = { "id", "source_chain", "target_chain", "type" };

    public const string CommittedReportCounterHelp =
        "Number of committed reports (by id, source_chain, target_chain & type)";

    // ===================== Histogram =====================
    public const string ExecutionDurationHistogram = "task_execution_duration";
    public static readonly string[] ExecutionDurationHistogramLabels = { "id", "source_chain", "target_chain", "type" };
    public const string ExecutionDurationHistogramHelp = "Time between task start and commit (seconds)";

    // ===================== Other =====================
    public const int MaxRetries = 5;
    public const int RetryDelayMs = 2000;
    public const int AddressDelayMs = 1000;
    public const string ChainTon = "ton";
    public const string ChainEvm = "evm";
    public const string ChainAelf = "aelf";
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