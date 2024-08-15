namespace AetherLink.Worker.Core.Reporter.MetricsDefinition;

public static class ReportMetricsDefinition
{
    public const string ReportGaugeName = "report";
    public const string ObservationTotalCounterName = "observation_total";
    public static readonly string[] ReportGaugeLabels = { "ChainId", "ReportId", "Epoch" };
}