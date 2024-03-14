namespace AetherLink.Worker.Core.Reporter.MetricsDefinition;

public class VRFMetricsDefinition
{
    public const string VRFGaugeName = "vrf";
    public const string VRFSumName = "vrf_sum";
    public static readonly string[] VRFGaugeLabels = { "chain_id", "request_id", "key_hash", "time_type" };
    public static readonly string[] VRFSumLabels = { "chain_id", "request_id", "key_hash", "time_type" };
    public const string GenerateTimeTypeLabel = "generate";
    public const string ExecuteTimeTypeLabel = "execute";
}