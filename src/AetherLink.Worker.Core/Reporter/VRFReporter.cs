using AetherLink.Metric;
using Prometheus;
using Volo.Abp.DependencyInjection;
using Definition = AetherLink.Worker.Core.Reporter.MetricsDefinition.VRFMetricsDefinition;

namespace AetherLink.Worker.Core.Reporter;

public interface IVRFReporter
{
    void RecordVrfJob(string chainId, string requestId);
}

public class VRFReporter : IVRFReporter, ISingletonDependency
{
    private readonly Counter _vrfCounter;

    public VRFReporter()
    {
        _vrfCounter = MetricsReporter.RegistryCounters(Definition.VRFName, Definition.VRFLabels);
    }

    public void RecordVrfJob(string chainId, string requestId) => _vrfCounter.WithLabels(chainId, requestId).Inc();
}