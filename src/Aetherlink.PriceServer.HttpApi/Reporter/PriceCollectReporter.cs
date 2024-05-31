using AetherLink.Metric;
using Aetherlink.PriceServer.Dtos;
using Prometheus;
using Volo.Abp.DependencyInjection;

namespace AetherlinkPriceServer.Reporter;

public interface IPriceCollectReporter
{
    public void RecordPriceCollected(SourceType type, string tokenPair, long price);
}

public class PriceCollectReporter : IPriceCollectReporter, ISingletonDependency
{
    private readonly Gauge _priceCollectedGauge;

    public PriceCollectReporter()
    {
        _priceCollectedGauge = MetricsReporter.RegistryGauges(PriceCollectMetricsDefinition.PriceCollectGaugeName,
            PriceCollectMetricsDefinition.PriceCollectLabels);
    }

    public void RecordPriceCollected(SourceType type, string tokenPair, long price)
        => _priceCollectedGauge.WithLabels(type.ToString(), tokenPair).Set(price);
}

// public interface IPriceFeedsReporter
// {
//     public void RecordPrice(string tokenPair, double price);
// }
//
// public class PriceFeedsReporter : IPriceFeedsReporter, ISingletonDependency
// {
//     private readonly Gauge _priceGauge;
//
//     public PriceFeedsReporter()
//     {
//         _priceGauge = MetricsReporter.RegistryGauges(Definition.PriceFeedsGaugeName, Definition.PriceLabels);
//     }
//
//     public void RecordPrice(string tokenPair, double price) => _priceGauge.WithLabels(tokenPair).Set(price);
// }