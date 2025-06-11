using System.Collections.Concurrent;
using AetherLink.Metric;
using NUglify.Helpers;
using Prometheus;
using Volo.Abp.DependencyInjection;
using Definition = AetherlinkPriceServer.Reporter.PriceQueryMetricsDefinition;

namespace AetherlinkPriceServer.Reporter;

public interface IPriceQueryReporter
{
    public void RecordPriceQueriedTotal(string appId, string router);
    public ITimer GetPriceRequestLatencyTimer(string appId, string router);
    public void RecordAggregatedPriceQueriedTotal(string appId, string tokenPair, string type);
    public void ReportAppQueriedRequestsMetrics();
}

public class PriceQueryReporter : IPriceQueryReporter, ISingletonDependency
{
    private readonly Gauge _appRequests;
    private readonly Histogram _priceRequestLatency;
    private readonly Counter _priceQueriedRequestsTotal;
    private readonly Counter _aggregatedPriceRequestsTotal;
    private readonly ConcurrentDictionary<string, double> _appRequestsMap = new();

    public PriceQueryReporter()
    {
        _priceQueriedRequestsTotal = MetricsReporter.RegistryCounters(Definition.PriceQueryRequestsTotalName,
            Definition.PriceQueryRequestsTotalLabels);
        _appRequests = MetricsReporter.RegistryGauges(Definition.AppRequestsName,
            Definition.AppRequestsLabels);
        _priceRequestLatency = MetricsReporter.RegistryHistograms(Definition.PriceRequestLatencyName,
            Definition.PriceRequestLatencyLabels,
            Definition.PriceRequestLatencyHelp,
            Definition.PriceRequestLatencyBuckets);
        _aggregatedPriceRequestsTotal = MetricsReporter.RegistryCounters(Definition.AggregatedPriceRequestsTotalName,
            Definition.AggregatedPriceRequestsTotalLabels);
    }

    public void RecordPriceQueriedTotal(string appId, string router)
    {
        _priceQueriedRequestsTotal.WithLabels(appId, router).Inc();

        if (!_appRequestsMap.TryGetValue(appId, out var app)) _appRequestsMap[appId] = 1;
        else _appRequestsMap[appId] = app + 1;
    }

    public void ReportAppQueriedRequestsMetrics()
    {
        _appRequestsMap.ForEach(a =>
        {
            _appRequests.WithLabels(a.Key).Set(a.Value);
            _appRequestsMap[a.Key] = 0;
        });
    }

    public ITimer GetPriceRequestLatencyTimer(string appId, string router)
        => _priceRequestLatency.WithLabels(appId, router).NewTimer();

    public void RecordAggregatedPriceQueriedTotal(string appId, string tokenPair, string type)
        => _aggregatedPriceRequestsTotal.WithLabels(appId, tokenPair, type).Inc();
}