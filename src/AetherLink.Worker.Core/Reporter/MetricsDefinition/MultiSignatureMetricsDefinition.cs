namespace AetherLink.Worker.Core.Reporter.MetricsDefinition;

public class MultiSignatureMetricsDefinition
{
    public const string MultiSignatureCounterName = "multi_signature";
    public const string MultiSignatureResultCounterName = "multi_signature_result";

    public static readonly string[] MultiSignatureResultCounterLabels =
        { "ChainId", "ReportId", "Epoch", "Index", "Result" };
}