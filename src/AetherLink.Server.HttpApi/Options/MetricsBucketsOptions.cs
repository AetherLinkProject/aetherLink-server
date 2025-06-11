namespace AetherLink.Server.HttpApi.Options;

public class MetricsBucketsOptions
{
    public double[] ExecutionDurationBuckets { get; set; } = { 40, 50, 60, 80, 100, 120, 180, 300 };
}