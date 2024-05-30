using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prometheus;
using Volo.Abp.Modularity;

namespace AetherLink.Metric;

public class AetherLinkMetricModule : AbpModule
{
    private void ConfigureMetrics(ServiceConfigurationContext context, IConfiguration configuration)
    {
        // var metricsOption = configuration.GetSection("Metrics").Get<MetricsOption>();
        // context.Services.AddMetricServer(options => { options.Port = metricsOption.Port; });
        // context.Services.AddHealthChecks();
    }
}