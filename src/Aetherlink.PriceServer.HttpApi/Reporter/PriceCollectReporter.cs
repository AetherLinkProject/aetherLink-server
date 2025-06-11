using AetherLink.Metric;
using Aetherlink.PriceServer.Dtos;
using Prometheus;
using Volo.Abp.DependencyInjection;
using Definition = AetherlinkPriceServer.Reporter.PriceCollectMetricsDefinition;

namespace AetherlinkPriceServer.Reporter;

public interface IPriceCollectReporter
{
    public void RecordPriceCollected(SourceType type, string tokenPair, long price);
    public ITimer GetPriceCollectLatencyTimer(SourceType type, string tokenPair);
    
}

public class PriceCollectReporter : IPriceCollectReporter, ISingletonDependency
{
    private readonly Gauge _priceCollectedGauge;
    private readonly Histogram _priceCollectLatency;

    public PriceCollectReporter()
    {
        _priceCollectedGauge = MetricsReporter.RegistryGauges(Definition.PriceCollectGaugeName,
            Definition.PriceCollectLabels);
        _priceCollectLatency = MetricsReporter.RegistryHistograms(Definition.ThirdPartyCollectLatencyName,
            Definition.ThirdPartyCollectLabels);
    }

    public void RecordPriceCollected(SourceType type, string tokenPair, long price)
        => _priceCollectedGauge.WithLabels(type.ToString(), tokenPair.ToUpper()).Set(price);

    public ITimer GetPriceCollectLatencyTimer(SourceType type, string tokenPair)
        => _priceCollectLatency.WithLabels(type.ToString(), tokenPair).NewTimer();
}