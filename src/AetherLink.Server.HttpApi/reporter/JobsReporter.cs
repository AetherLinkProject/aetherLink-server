using Prometheus;
using AetherLink.Server.HttpApi.Constants;

namespace AetherLink.Server.HttpApi.Reporter
{
    public class JobsReporter
    {
        private readonly Counter _startedRequestCounter;
        private readonly Counter _committedReportCounter;
        private readonly Histogram _executionDurationHistogram;

        public JobsReporter()
        {
            _startedRequestCounter = Metrics.CreateCounter(
                MetricsConstants.StartedRequestCounter,
                "Number of started business tasks (by chain & type)",
                new CounterConfiguration
                {
                    LabelNames = new[] { "chain", "task_type" }
                });
            _committedReportCounter = Metrics.CreateCounter(
                MetricsConstants.CommittedReportCounter,
                "Number of committed reports",
                new CounterConfiguration
                {
                    LabelNames = new[] { "chain", "type" }
                });
            _executionDurationHistogram = Metrics.CreateHistogram(
                MetricsConstants.ExecutionDurationHistogram,
                "Time between task start and commit (seconds)",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "chain", "type" },
                    Buckets = Histogram.ExponentialBuckets(60, 1.2, 15) // 60s ~ 646s, 60-240s
                });
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