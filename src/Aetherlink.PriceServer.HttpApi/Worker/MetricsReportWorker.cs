using System.Threading.Tasks;
using AetherlinkPriceServer.Options;
using AetherlinkPriceServer.Reporter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherlinkPriceServer.Worker;

public class MetricsReportWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly IPriceQueryReporter _reporter;
    private readonly ILogger<MetricsReportWorker> _logger;

    public MetricsReportWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IPriceQueryReporter reporter, IOptionsSnapshot<MetricsReportOption> option,
        ILogger<MetricsReportWorker> logger) : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _reporter = reporter;
        _logger.LogDebug($"get MetricsReportOption: {JsonConvert.SerializeObject(option.Value)}");
        Timer.Period = option.Value.Interval;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.LogDebug("Start Report AetherLink Price server query metrics.");
        _reporter.ReportAppQueriedRequestsMetrics();
    }
}