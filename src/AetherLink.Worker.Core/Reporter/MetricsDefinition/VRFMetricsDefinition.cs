namespace AetherLink.Worker.Core.Reporter.MetricsDefinition;

public class VRFMetricsDefinition
{
    public const string JobCounterName = "vrf";
    public static readonly string[] JobCounterLabels = { "chain_id", "request_id" };

    public const string ExecuteTimeGaugeName = "vrf_execute_time";
    public static readonly string[] ExecuteTimeLabels = { "chain_id", "request_id" };
}