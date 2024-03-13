using Prometheus;
using Volo.Abp.DependencyInjection;
using Definition = AetherLink.Worker.Core.Reporter.MetricsDefinition.DataFeedsMetricsDefinition;

namespace AetherLink.Worker.Core.Reporter;

public interface IDataFeedsReporter
{
    void RecordPrice(string currencyPair, double price);
}

public class DataFeedsReporter : IDataFeedsReporter, ISingletonDependency
{
    private readonly Gauge _priceGauge;

    public DataFeedsReporter()
    {
        _priceGauge = MetricsReporter.RegistryGauges(Definition.DataFeedsGaugeName, Definition.PriceLabels);
    }

    public void RecordPrice(string currencyPair, double price) => _priceGauge.WithLabels(currencyPair).Set(price);
}