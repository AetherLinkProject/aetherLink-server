using Prometheus;

namespace AetherLink.Metric;

public class MetricsReporter
{
    private const string Prefix = "aetherlink_";

    public static Gauge RegistryGauges(string gaugeName, string[] labels, string help = "")
        => Metrics.CreateGauge(Prefix + gaugeName, help, labels);

    public static Counter RegistryCounters(string counterName, string[] labels, string help = "")
        => Metrics.CreateCounter(Prefix + counterName, help, labels);

    public static Histogram RegistryHistograms(string histogramName, string[] labels, string help = "",
        params double[] buckets) => buckets is { Length: > 0 }
        ? Metrics.CreateHistogram(Prefix + histogramName, help, labels,
            new HistogramConfiguration { Buckets = buckets })
        : Metrics.CreateHistogram(Prefix + histogramName, help, labels);
}