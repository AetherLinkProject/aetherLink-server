using Prometheus;
using Volo.Abp.DependencyInjection;
using Definition = AetherLink.Worker.Core.Reporter.MetricsDefinition.JobCommonMetricsDefinition;

namespace AetherLink.Worker.Core.Reporter;

public interface IJobCommonReporter
{
    void RecordVrfJob(string chainId, string requestId, double executeTime);
    void RecordDatafeedJob(string chainId, string requestId, double executeTime);
}

public class JobCommonReporter : IJobCommonReporter, ISingletonDependency
{
    private readonly Gauge _jobGauge;

    public JobCommonReporter()
    {
        _jobGauge = MetricsReporter.RegistryGauges(Definition.JobGaugeName, Definition.JobCommonLabels);
    }

    public void RecordVrfJob(string chainId, string requestId, double executeTime) =>
        _jobGauge.WithLabels(chainId, requestId, Definition.VrfTypeLabel).Set(executeTime);

    public void RecordDatafeedJob(string chainId, string requestId, double executeTime)
        => _jobGauge.WithLabels(chainId, requestId, Definition.DatafeedTypeLabel).Set(executeTime);
}