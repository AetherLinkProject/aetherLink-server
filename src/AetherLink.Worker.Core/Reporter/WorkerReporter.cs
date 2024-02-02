using Prometheus;
using Volo.Abp.DependencyInjection;
using Definition = AetherLink.Worker.Core.Reporter.MetricsDefinition.WorkerMetricsDefinition;

namespace AetherLink.Worker.Core.Reporter;

public interface IWorkerReporter
{
    void RecordOracleJobAsync(string chainId, int count);
    void RecordTransmittedAsync(string chainId, int count);
    void RecordCanceledAsync(string chainId, int count);
}

public class WorkerReporter : IWorkerReporter, ISingletonDependency
{
    private readonly Gauge _workerGauge;

    public WorkerReporter()
    {
        _workerGauge = MetricsReporter.RegistryGauges(Definition.SearchWorkerGaugeName, Definition.WorkerGaugeLabels);
    }

    public void RecordOracleJobAsync(string chainId, int count) =>
        _workerGauge.WithLabels(chainId, Definition.OracleJobGaugeLabel).Inc(count);

    public void RecordTransmittedAsync(string chainId, int count) =>
        _workerGauge.WithLabels(chainId, Definition.TransmittedGaugeLabel).Inc(count);

    public void RecordCanceledAsync(string chainId, int count) =>
        _workerGauge.WithLabels(chainId, Definition.CanceledGaugeLabel).Inc(count);
}