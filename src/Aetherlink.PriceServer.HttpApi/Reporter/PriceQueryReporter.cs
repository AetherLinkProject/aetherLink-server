using AetherLink.Metric;
using Prometheus;
using Volo.Abp.DependencyInjection;

namespace AetherlinkPriceServer.Reporter;

public interface IPriceQueryReporter
{
    public void RecordPriceQueried(string appId = "", string methodName = "");
}

public class PriceQueryReporter : IPriceQueryReporter, ISingletonDependency
{
    private readonly Counter _priceQueriedCounter;

    public PriceQueryReporter()
    {
        _priceQueriedCounter = MetricsReporter.RegistryCounters(PriceQueryMetricsDefinition.PriceQueryCounterName,
            PriceQueryMetricsDefinition.PriceQueryLabels);
    }

    public void RecordPriceQueried(string appId, string methodName)
        => _priceQueriedCounter.WithLabels(appId, methodName).Inc();
}