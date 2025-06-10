using Prometheus;
using AetherLink.Server.HttpApi.Constants;
using Volo.Abp.DependencyInjection;
using AetherLink.Metric;

namespace AetherLink.Server.HttpApi.Reporter
{
    public class JobsReporter : ISingletonDependency
    {
        private readonly Counter _startedRequestCounter;
        private readonly Counter _committedReportCounter;
        private readonly Histogram _executionDurationHistogram;

        public JobsReporter()
        {
            _startedRequestCounter = MetricsReporter.RegistryCounters(
                MetricsConstants.StartedRequestCounter,
                MetricsConstants.StartedRequestCounterLabels,
                MetricsConstants.StartedRequestCounterHelp);
            _committedReportCounter = MetricsReporter.RegistryCounters(
                MetricsConstants.CommittedReportCounter,
                MetricsConstants.CommittedReportCounterLabels,
                MetricsConstants.CommittedReportCounterHelp);
            _executionDurationHistogram = MetricsReporter.RegistryHistograms(
                MetricsConstants.ExecutionDurationHistogram,
                MetricsConstants.ExecutionDurationHistogramLabels,
                MetricsConstants.ExecutionDurationHistogramHelp);
        }

        public void ReportStartedRequest(string chain, string taskType)
        {
            _startedRequestCounter.WithLabels(chain, taskType).Inc(1);
        }

        public void ReportCommittedReport(string chain, string type)
        {
            _committedReportCounter.WithLabels(chain, type).Inc(1);
        }

        public void ReportExecutionDuration(string chain, string type, double durationSeconds)
        {
            _executionDurationHistogram.WithLabels(chain, type).Observe(durationSeconds);
        }
    }
}