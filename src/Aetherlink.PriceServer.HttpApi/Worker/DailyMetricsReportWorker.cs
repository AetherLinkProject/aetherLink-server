using System;
using System.Threading.Tasks;
using AetherlinkPriceServer.Reporter;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherlinkPriceServer.Worker;

public class DailyMetricsReportWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly IPriceQueryReporter _reporter;

    public DailyMetricsReportWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IPriceQueryReporter reporter) : base(timer, serviceScopeFactory)
    {
        _reporter = reporter;
        // Timer.Period = (int)TimeSpan.FromDays(1).TotalMilliseconds;
        Timer.Period = 1000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _reporter.ReportAppQueriedRequestsMetrics();
    }
}