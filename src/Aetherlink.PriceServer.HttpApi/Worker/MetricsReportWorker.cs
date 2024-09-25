using System.Threading.Tasks;
using AetherlinkPriceServer.Options;
using AetherlinkPriceServer.Reporter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherlinkPriceServer.Worker;

public class MetricsReportWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly MetricsReportOption _option;
    private readonly IPriceQueryReporter _reporter;

    public MetricsReportWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IPriceQueryReporter reporter, IOptions<MetricsReportOption> option) : base(timer, serviceScopeFactory)
    {
        _reporter = reporter;
        _option = option.Value;
        Timer.Period = _option.Interval;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _reporter.ReportAppQueriedRequestsMetrics();
    }
}