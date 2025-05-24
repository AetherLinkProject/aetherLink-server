using AetherLink.Metric;
using Prometheus;
using AetherLink.Server.HttpApi.Constants;

namespace AetherLink.Server.HttpApi.Reporter;

public class BalanceReporter
{
    private readonly Gauge _balanceGauge;

    public BalanceReporter()
    {
        _balanceGauge = MetricsReporter.RegistryGauges(MetricsConstants.BalanceGaugeName, MetricsConstants.BalanceGaugeLabels, "Chain address balance monitor");
    }

    public void SetBalance(string chain, string address, decimal balance)
    {
        _balanceGauge.WithLabels(chain, address).Set((double)balance);
    }
} 