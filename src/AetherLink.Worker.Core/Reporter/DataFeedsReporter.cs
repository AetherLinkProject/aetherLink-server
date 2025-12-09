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

    public DataFeedsReporter()
    {
        _jobCounter = MetricsReporter.RegistryCounters(Definition.JobCounterName, Definition.JobCounterLabels);
    }

    public void RecordDatafeedJob(string chainId, string requestId, long epoch, int roundId, double executeTime)
        => _jobCounter.WithLabels(chainId, requestId, epoch.ToString(), roundId.ToString()).Inc(executeTime);
}