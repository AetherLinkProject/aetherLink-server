using AetherLink.Metric;
using Prometheus;
using Volo.Abp.DependencyInjection;
using Definition = AetherlinkPriceServer.Reporter.PriceQueryMetricsDefinition;

namespace AetherlinkPriceServer.Reporter;

public interface IPriceQueryReporter
{
    public void RecordPriceQueriedTotal(string appId, string router);
    public ITimer GetPriceRequestLatencyTimer(string appId, string router);
    public void RecordAggregatedPriceQueriedTotal(string appId, string tokenPair, string type);
}

public class PriceQueryReporter : IPriceQueryReporter, ISingletonDependency
{
    private readonly Histogram _priceRequestLatency;
    private readonly Counter _priceQueriedRequestsTotal;
    private readonly Counter _aggregatedPriceRequestsTotal;

    public PriceQueryReporter()
    {
        _priceQueriedRequestsTotal = MetricsReporter.RegistryCounters(Definition.PriceQueryRequestsTotalName,
            Definition.PriceQueryRequestsTotalLabels);
        _priceRequestLatency = MetricsReporter.RegistryHistograms(Definition.PriceRequestLatencyName,
            Definition.PriceRequestLatencyLabels);
        _aggregatedPriceRequestsTotal = MetricsReporter.RegistryCounters(Definition.AggregatedPriceRequestsTotalName,
            Definition.AggregatedPriceRequestsTotalLabels);
    }

    public void RecordPriceQueriedTotal(string appId, string router)
        => _priceQueriedRequestsTotal.WithLabels(appId, router).Inc();

    public ITimer GetPriceRequestLatencyTimer(string appId, string router)
        => _priceRequestLatency.WithLabels(appId, router).NewTimer();

    public void RecordAggregatedPriceQueriedTotal(string appId, string tokenPair, string type)
        => _aggregatedPriceRequestsTotal.WithLabels(appId, tokenPair, type).Inc();
}