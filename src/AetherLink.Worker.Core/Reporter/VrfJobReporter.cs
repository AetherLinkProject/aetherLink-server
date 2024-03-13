using Prometheus;
using Volo.Abp.DependencyInjection;
using Definition = AetherLink.Worker.Core.Reporter.MetricsDefinition.VrfJobMetricsDefinition;

namespace AetherLink.Worker.Core.Reporter;

public interface IVrfJobReporter
{
    void RecordVrfJob(string chainId, string requestId);
}

public class VrfJobReporter : IVrfJobReporter, ISingletonDependency
{
    private readonly Counter _vrfCounter;

    public VrfJobReporter()
    {
        _vrfCounter = MetricsReporter.RegistryCounters(Definition.VrfJobCounterName, Definition.VrfJobCounterLabels);
    }

    public void RecordVrfJob(string chainId, string requestId) => _vrfCounter.WithLabels(chainId, requestId).Inc();
}