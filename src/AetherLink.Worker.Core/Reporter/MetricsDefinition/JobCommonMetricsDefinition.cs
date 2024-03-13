namespace AetherLink.Worker.Core.Reporter.MetricsDefinition;

public class JobCommonMetricsDefinition
{
    public const string JobGaugeName = "job_common";
    public static readonly string[] JobCommonLabels = { "chain_id", "request_id", "job_type" };
    public const string DatafeedTypeLabel = "datafeed";
    public const string VrfTypeLabel = "vrf";
}