using Prometheus;
using Volo.Abp.DependencyInjection;
using Definition = AetherLink.Worker.Core.Reporter.MetricsDefinition.VRFMetricsDefinition;

namespace AetherLink.Worker.Core.Reporter;

public interface IVRFReporter
{
    void RecordVrfJob(string chainId, string requestId, string keyHash, double generationTime,
        double executeTime);
}

public class VRFReporter : IVRFReporter, ISingletonDependency
{
    private readonly Gauge _jobGauge;

    public VRFReporter()
    {
        _jobGauge = MetricsReporter.RegistryGauges(Definition.VRFGaugeName, Definition.VRFGaugeLabels);
    }

    public void RecordVrfJob(string chainId, string requestId, string keyHash, double generationTime,
        double executeTime)
    {
        _jobGauge.WithLabels(chainId, requestId, keyHash, Definition.ExecuteTimeTypeLabel).Set(executeTime);
        _jobGauge.WithLabels(chainId, requestId, keyHash, Definition.GenerateTimeTypeLabel).Set(generationTime);
    }
}