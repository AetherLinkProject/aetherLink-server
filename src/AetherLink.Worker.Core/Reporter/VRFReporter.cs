using AetherLink.Metric;
using Prometheus;
using Volo.Abp.DependencyInjection;
using Definition = AetherLink.Worker.Core.Reporter.MetricsDefinition.VRFMetricsDefinition;

namespace AetherLink.Worker.Core.Reporter;

public interface IVRFReporter
{
    void RecordVrfExecuteTime(string chainId, string requestId, double duration);
}

public class VRFReporter : IVRFReporter, ISingletonDependency
{
    private readonly Counter _jobCounter;
    private readonly Gauge _executeGauge;

    public VRFReporter()
    {
        _jobCounter = MetricsReporter.RegistryCounters(Definition.JobCounterName, Definition.JobCounterLabels);
        _executeGauge = MetricsReporter.RegistryGauges(Definition.ExecuteTimeGaugeName, Definition.ExecuteTimeLabels);
    }

    public void RecordVrfExecuteTime(string chainId, string requestId, double duration)
    {
        _jobCounter.WithLabels(chainId, requestId).Inc();
        _executeGauge.WithLabels(chainId, requestId).Set(duration);
    }
}