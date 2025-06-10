using AetherLink.Metric;
using Prometheus;
using AetherLink.Server.HttpApi.Constants;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Server.HttpApi.Reporter
{
    public class CrossChainReporter : ISingletonDependency
    {
        private readonly Counter _crossChainRequestCounter;
        private readonly Counter _crossChainQueryHitCounter;
        private readonly Counter _crossChainQueryTotalCounter;

        public CrossChainReporter()
        {
            _crossChainRequestCounter = MetricsReporter.RegistryCounters(
                MetricsConstants.CrossChainRequestCounter,
                MetricsConstants.CrossChainRequestCounterLabels,
                MetricsConstants.CrossChainRequestCounterHelp);
            _crossChainQueryHitCounter = MetricsReporter.RegistryCounters(
                MetricsConstants.CrossChainQueryHitCounter,
                MetricsConstants.CrossChainQueryHitCounterLabels,
                MetricsConstants.CrossChainQueryHitCounterHelp);
            _crossChainQueryTotalCounter = MetricsReporter.RegistryCounters(
                MetricsConstants.CrossChainQueryTotalCounter,
                MetricsConstants.CrossChainQueryTotalCounterLabels,
                MetricsConstants.CrossChainQueryTotalCounterHelp);
        }

        public void ReportCrossChainRequest(string messageId, string sourceChain, string targetChain)
        {
            _crossChainRequestCounter.WithLabels(messageId, sourceChain, targetChain).Inc();
        }

        public void ReportCrossChainQueryHitCount(string id, string chain, bool hit)
        {
            _crossChainQueryHitCounter.WithLabels(id ?? string.Empty, chain ?? string.Empty, hit ? "1" : "0").Inc();
        }

        public void ReportCrossChainQueryTotalCount(string id)
        {
            _crossChainQueryTotalCounter.WithLabels(id ?? string.Empty).Inc();
        }
    }
}