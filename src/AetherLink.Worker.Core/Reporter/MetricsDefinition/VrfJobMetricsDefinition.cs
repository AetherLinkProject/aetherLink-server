namespace AetherLink.Worker.Core.Reporter.MetricsDefinition;

public class VrfJobMetricsDefinition
{
    public const string VrfJobCounterName = "vrf_job";
    public static readonly string[] VrfJobCounterLabels = { "chain_id", "request_id" };
}