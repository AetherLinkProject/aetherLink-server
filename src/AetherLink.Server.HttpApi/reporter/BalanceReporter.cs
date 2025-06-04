using AetherLink.Metric;
using AetherLink.Server.HttpApi.Constants;
using Prometheus;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Server.HttpApi.Reporter;

public class BalanceReporter : ISingletonDependency
{
    private readonly Gauge _balanceGauge;

    public BalanceReporter()
    {
        _balanceGauge = MetricsReporter.RegistryGauges(
            MetricsConstants.BalanceGaugeName,
            MetricsConstants.BalanceGaugeLabels,
            "Chain address balance monitor");
    }

    public void SetBalance(string chain, string address, decimal balance)
    {
        _balanceGauge.WithLabels(chain, address).Set((double)balance);
    }
}