namespace AetherLink.Worker.Core.Reporter.MetricsDefinition;

public class WorkerMetricsDefinition
{
    public const string SearchWorkerGaugeName = "search_worker";
    public const string OracleJobGaugeLabel = "oracle_job";
    public const string TransmittedGaugeLabel = "transmitted";
    public const string CanceledGaugeLabel = "canceled";
    public static readonly string[] SearchGaugeLabels = { "chain_id", "type" };

    public const string SearchBlockHeightGaugeName = "search_block_height";
    public static readonly string[] SearchBlockHeightGaugeLabels = { "chain_id", "subscribe_type" };
    public const string ConfirmCounterLabel = "confirm";
    public const string UnconfirmedCounterLabel = "unconfirmed";
}