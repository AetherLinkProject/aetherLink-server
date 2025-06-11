using Prometheus;
using AetherLink.Server.HttpApi.Constants;
using Volo.Abp.DependencyInjection;
using AetherLink.Metric;
using Microsoft.Extensions.Options;
using AetherLink.Server.HttpApi.Options;

namespace AetherLink.Server.HttpApi.Reporter
{
    public class JobsReporter : ISingletonDependency
    {
        private readonly Counter _startedRequestCounter;
        private readonly Counter _committedReportCounter;
        private readonly Histogram _executionDurationHistogram;

        public JobsReporter(IOptions<MetricsBucketsOptions> options)
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
                MetricsConstants.ExecutionDurationHistogramHelp,
                options.Value.ExecutionDurationBuckets);
        }

        public void ReportStartedRequest(string id, string sourceChain, string targetChain, string type)
        {
            _startedRequestCounter.WithLabels(id, sourceChain, targetChain, type).Inc();
        }

        public void ReportCommittedReport(string id, string sourceChain, string targetChain, string type)
        {
            _committedReportCounter.WithLabels(id, sourceChain, targetChain, type).Inc();
        }

        public void ReportExecutionDuration(string id, string chain, string type, double durationSeconds)
        {
            _executionDurationHistogram.WithLabels(id, chain, type).Observe(durationSeconds);
        }
    }
}