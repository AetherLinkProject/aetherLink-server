using Prometheus;
using Volo.Abp.DependencyInjection;
using Definition = AetherLink.Worker.Core.Reporter.MetricsDefinition.DataFeedsMetricsDefinition;

namespace AetherLink.Worker.Core.Reporter;

public interface IDataFeedsReporter
{
    void RecordPrice(string currencyPair, double price);
    void RecordDatafeedJob(string chainId, string requestId, long epoch, int roundId, double executeTime);
}

public class DataFeedsReporter : IDataFeedsReporter, ISingletonDependency
{
    private readonly Gauge _priceGauge;
    private readonly Gauge _jobGauge;

    public DataFeedsReporter()
    {
        _priceGauge = MetricsReporter.RegistryGauges(Definition.DataFeedsGaugeName, Definition.PriceLabels);
        _jobGauge = MetricsReporter.RegistryGauges(Definition.DataFeedsGaugeName, Definition.DataFeedsJobLabels);
    }

    public void RecordPrice(string currencyPair, double price) => _priceGauge.WithLabels(currencyPair).Set(price);

    public void RecordDatafeedJob(string chainId, string requestId, long epoch, int roundId, double executeTime)
        => _jobGauge.WithLabels(chainId, requestId, epoch.ToString(), roundId.ToString()).Set(executeTime);
}