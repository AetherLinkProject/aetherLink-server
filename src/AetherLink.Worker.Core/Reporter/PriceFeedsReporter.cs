using AetherLink.Metric;
using Prometheus;
using Volo.Abp.DependencyInjection;
using Definition = AetherLink.Worker.Core.Reporter.MetricsDefinition.PriceFeedsMetricsDefinition;

namespace AetherLink.Worker.Core.Reporter;

public interface IPriceFeedsReporter
{
    public void RecordPrice(string currencyPair, double price);
}

public class PriceFeedsReporter : IPriceFeedsReporter, ISingletonDependency
{
    private readonly Gauge _priceGauge;

    public PriceFeedsReporter()
    {
        _priceGauge = MetricsReporter.RegistryGauges(Definition.PriceFeedsGaugeName, Definition.PriceLabels);
    }

    public void RecordPrice(string currencyPair, double price) => _priceGauge.WithLabels(currencyPair).Set(price);
}