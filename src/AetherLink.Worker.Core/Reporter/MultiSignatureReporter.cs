using AetherLink.Metric;
using AetherLink.Worker.Core.Reporter.MetricsDefinition;
using Prometheus;
using Volo.Abp.DependencyInjection;
using Definition = AetherLink.Worker.Core.Reporter.MetricsDefinition.MultiSignatureMetricsDefinition;

namespace AetherLink.Worker.Core.Reporter;

public interface IMultiSignatureReporter
{
    public void RecordMultiSignatureAsync(string chainId, string requestId, long epoch);

    public void RecordMultiSignatureProcessResultAsync(string chainId, string requestId, long epoch, int index,
        string result = "success");
}

public class MultiSignatureReporter : IMultiSignatureReporter, ISingletonDependency
{
    private readonly Counter _multiSignatureCounter;
    private readonly Counter _multiSignatureResultCounter;

    public MultiSignatureReporter()
    {
        _multiSignatureCounter = MetricsReporter.RegistryCounters(Definition.MultiSignatureCounterName,
            BasicMetricDefinition.BasicLabels);
        _multiSignatureResultCounter = MetricsReporter.RegistryCounters(Definition.MultiSignatureResultCounterName,
            Definition.MultiSignatureResultCounterLabels);
    }

    public void RecordMultiSignatureAsync(string chainId, string requestId, long epoch)
        => _multiSignatureCounter.WithLabels(chainId, requestId, epoch.ToString()).Inc();

    public void RecordMultiSignatureProcessResultAsync(string chainId, string requestId, long epoch, int index,
        string result) => _multiSignatureResultCounter
        .WithLabels(chainId, requestId, epoch.ToString(), index.ToString(), result).Inc();
}