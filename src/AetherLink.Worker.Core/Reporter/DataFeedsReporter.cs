using AetherLink.Metric;
using Prometheus;
using Volo.Abp.DependencyInjection;
using Definition = AetherLink.Worker.Core.Reporter.MetricsDefinition.DataFeedsMetricsDefinition;

namespace AetherLink.Worker.Core.Reporter;

public interface IDataFeedsReporter
{
    void RecordDatafeedJob(string chainId, string requestId, long epoch, int roundId, double executeTime);
}

public class DataFeedsReporter : IDataFeedsReporter, ISingletonDependency
{
    private readonly Counter _jobCounter;
    private readonly Gauge _executeGauge;

    public DataFeedsReporter()
    {
        _jobCounter = MetricsReporter.RegistryCounters(Definition.JobCounterName, Definition.JobLabels);
        _executeGauge = MetricsReporter.RegistryGauges(Definition.ExecuteTimeGaugeName, Definition.JobLabels);
    }

    public void RecordDatafeedJob(string chainId, string requestId, long epoch, int roundId, double executeTime)
    {
        _jobCounter.WithLabels(chainId, requestId, epoch.ToString(), roundId.ToString()).Inc();
        _executeGauge.WithLabels(chainId, requestId, epoch.ToString(), roundId.ToString()).Set(executeTime);
    }
}