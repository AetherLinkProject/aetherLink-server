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
    private readonly Counter _dataFeedsCounter;

    public DataFeedsReporter()
    {
        _dataFeedsCounter =
            MetricsReporter.RegistryCounters(Definition.DataFeedsSumName, Definition.DataFeedsJobSumLabels);
    }

    public void RecordDatafeedJob(string chainId, string requestId, long epoch, int roundId, double executeTime)
        => _dataFeedsCounter.WithLabels(chainId, requestId, epoch.ToString(), roundId.ToString()).Inc(executeTime);
}