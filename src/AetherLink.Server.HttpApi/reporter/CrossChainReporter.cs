using Prometheus;

namespace AetherLink.Server.HttpApi.Reporter
{
    public interface ICrossChainReporter
    {
        void ReportCrossChainRequest(string messageId, string sourceChain, string targetChain);
        void ReportCrossChainQueryHitCount(string id, string chain, bool hit);
        void ReportCrossChainQueryTotalCount(string id);
    }

    public class CrossChainReporter : ICrossChainReporter
    {
        private readonly Counter _crossChainRequestCounter;
        private readonly Counter _crossChainQueryHitCounter;
        private readonly Counter _crossChainQueryTotalCounter;

        public CrossChainReporter()
        {
            _crossChainRequestCounter = Metrics.CreateCounter(
                "cross_chain_request",
                "Number of crosschain requests (by MessageId, SourceChain, TargetChain)",
                new CounterConfiguration
                {
                    LabelNames = new[] { "MessageId", "SourceChain", "TargetChain" }
                });
            _crossChainQueryHitCounter = Metrics.CreateCounter(
                "cross_chain_query_hit_count",
                "Number of cross-chain query hits (by id, chain, hit)",
                new CounterConfiguration
                {
                    LabelNames = new[] { "id", "chain", "hit" }
                });
            _crossChainQueryTotalCounter = Metrics.CreateCounter(
                "cross_chain_query_total_count",
                "Total number of cross-chain queries (by id)",
                new CounterConfiguration
                {
                    LabelNames = new[] { "id" }
                });
        }

        public void ReportCrossChainRequest(string messageId, string sourceChain, string targetChain)
        {
            _crossChainRequestCounter.WithLabels(messageId, sourceChain, targetChain).Inc(1);
        }

        public void ReportCrossChainQueryHitCount(string id, string chain, bool hit)
        {
            _crossChainQueryHitCounter.WithLabels(id ?? string.Empty, chain ?? string.Empty, hit ? "1" : "0").Inc(1);
        }

        public void ReportCrossChainQueryTotalCount(string id)
        {
            _crossChainQueryTotalCounter.WithLabels(id ?? string.Empty).Inc(1);
        }
    }
} 