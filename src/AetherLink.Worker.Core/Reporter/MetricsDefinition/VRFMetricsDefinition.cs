namespace AetherLink.Worker.Core.Reporter.MetricsDefinition;

public class VRFMetricsDefinition
{
    public const string VRFName = "vrf";
    public static readonly string[] VRFLabels = { "chain_id", "request_id", "time_type" };
    public const string ExecuteTimeTypeLabel = "execute";
}