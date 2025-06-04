using Prometheus;

namespace AetherLink.Server.HttpApi.Reporter
{
    public interface IJobsReporter
    {
        void ReportStartedRequest(string chain, string taskType);
        void ReportCommittedReport(string chain, string type);
        void ReportExecutionDuration(string chain, string type, double durationSeconds);
    }

    public class JobsReporter : IJobsReporter
    {
        private readonly Counter _startedRequestCounter;
        private readonly Counter _committedReportCounter;
        private readonly Histogram _executionDurationHistogram;

        public JobsReporter()
        {
            _startedRequestCounter = Metrics.CreateCounter(
                "started_request",
                "Number of started business tasks (by chain & type)",
                new CounterConfiguration
                {
                    LabelNames = new[] { "chain", "task_type" }
                });
            _committedReportCounter = Metrics.CreateCounter(
                "committed_report",
                "Number of committed reports",
                new CounterConfiguration
                {
                    LabelNames = new[] { "chain", "type" }
                });
            _executionDurationHistogram = Metrics.CreateHistogram(
                "task_execution_duration",
                "Time between task start and commit (seconds)",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "chain", "type" },
                    Buckets = Histogram.ExponentialBuckets(0.5, 2, 15) // 0.5s ~ 8192s
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