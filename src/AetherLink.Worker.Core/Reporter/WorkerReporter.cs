using AetherLink.Metric;
using Prometheus;
using Volo.Abp.DependencyInjection;
using Definition = AetherLink.Worker.Core.Reporter.MetricsDefinition.WorkerMetricsDefinition;

namespace AetherLink.Worker.Core.Reporter;

public interface IWorkerReporter
{
    void RecordConfirmBlockHeight(string chainId, long start, long end);
    void RecordUnconfirmedBlockHeight(string chainId, long start, long end);
    void RecordOracleJobAsync(string chainId, int count);
    void RecordTransmittedAsync(string chainId, int count);
    void RecordCanceledAsync(string chainId, int count);
}

public class WorkerReporter : IWorkerReporter, ISingletonDependency
{
    private readonly Gauge _workerGauge;
    private readonly Gauge _blockHeightGauge;

    public WorkerReporter()
    {
        _workerGauge = MetricsReporter.RegistryGauges(Definition.SearchWorkerGaugeName, Definition.SearchGaugeLabels);
        _blockHeightGauge = MetricsReporter.RegistryGauges(Definition.SearchBlockHeightGaugeName,
            Definition.SearchBlockHeightGaugeLabels);
    }

    public void RecordConfirmBlockHeight(string chainId, long start, long end)
    {
        _blockHeightGauge.WithLabels(chainId, Definition.ConfirmStartCounterLabel).Set(start);
        _blockHeightGauge.WithLabels(chainId, Definition.ConfirmEndCounterLabel).Set(end);
    }

    public void RecordUnconfirmedBlockHeight(string chainId, long start, long end)
    {
        _blockHeightGauge.WithLabels(chainId, Definition.UnconfirmedStartCounterLabel).Set(start);
        _blockHeightGauge.WithLabels(chainId, Definition.UnconfirmedEndCounterLabel).Set(end);
    }

    public void RecordOracleJobAsync(string chainId, int count) =>
        _workerGauge.WithLabels(chainId, Definition.OracleJobGaugeLabel).Inc(count);

    public void RecordTransmittedAsync(string chainId, int count) =>
        _workerGauge.WithLabels(chainId, Definition.TransmittedGaugeLabel).Inc(count);

    public void RecordCanceledAsync(string chainId, int count) =>
        _workerGauge.WithLabels(chainId, Definition.CanceledGaugeLabel).Inc(count);
}