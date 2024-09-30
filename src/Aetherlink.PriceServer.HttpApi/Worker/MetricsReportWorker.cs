using System.Threading.Tasks;
using AetherlinkPriceServer.Options;
using AetherlinkPriceServer.Reporter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherlinkPriceServer.Worker;

public class MetricsReportWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly MetricsReportOption _option;
    private readonly IPriceQueryReporter _reporter;
    private readonly ILogger<MetricsReportWorker> _logger;

    public MetricsReportWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IPriceQueryReporter reporter, IOptions<MetricsReportOption> option, ILogger<MetricsReportWorker> logger) : base(
        timer, serviceScopeFactory)
    {
        _logger = logger;
        _reporter = reporter;
        _option = option.Value;
        Timer.Period = _option.Interval;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.LogDebug("Start Report AetherLink Price server query metrics.");
        _reporter.ReportAppQueriedRequestsMetrics();
    }
}