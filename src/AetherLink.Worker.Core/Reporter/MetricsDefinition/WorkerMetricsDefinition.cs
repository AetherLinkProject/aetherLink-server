namespace AetherLink.Worker.Core.Reporter.MetricsDefinition;

public class WorkerMetricsDefinition
{
    public const string SearchWorkerGaugeName = "search_worker";
    public const string OracleJobGaugeLabel = "oracle_job";
    public const string TransmittedGaugeLabel = "transmitted";
    public const string CanceledGaugeLabel = "canceled";
    public static readonly string[] WorkerGaugeLabels = { "chain_id", "type" };
}