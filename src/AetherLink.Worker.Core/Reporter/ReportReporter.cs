using AetherLink.Metric;
using AetherLink.Worker.Core.Reporter.MetricsDefinition;
using Prometheus;
using Volo.Abp.DependencyInjection;
using Definition = AetherLink.Worker.Core.Reporter.MetricsDefinition.ReportMetricsDefinition;

namespace AetherLink.Worker.Core.Reporter;

public interface IReportReporter
{
    public void RecordReportAsync(string chainId, string reportId, long epoch);
}

public class ReportReporter : IReportReporter, ISingletonDependency
{
    private readonly Counter _reportCounter;

    public ReportReporter()
    {
        _reportCounter = MetricsReporter.RegistryCounters(Definition.ObservationTotalCounterName,
            BasicMetricDefinition.BasicLabels);
    }

    public void RecordReportAsync(string chainId, string reportId, long epoch)
        => _reportCounter.WithLabels(chainId, reportId, epoch.ToString()).Inc();
}