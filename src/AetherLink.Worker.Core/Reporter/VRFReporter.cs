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
    private readonly Counter _vrfCounter;

    public VRFReporter()
    {
        _vrfCounter = MetricsReporter.RegistryCounters(Definition.VRFSumName, Definition.VRFSumLabels);
    }

    public void RecordVrfJob(string chainId, string requestId, string keyHash, double generationTime,
        double executeTime)
    {
        _vrfCounter.WithLabels(chainId, requestId, keyHash, Definition.ExecuteTimeTypeLabel).Inc(executeTime);
        _vrfCounter.WithLabels(chainId, requestId, keyHash, Definition.GenerateTimeTypeLabel).Inc(generationTime);
    }
}