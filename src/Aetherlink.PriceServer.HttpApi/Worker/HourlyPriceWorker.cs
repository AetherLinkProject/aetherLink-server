using System;
using System.Linq;
using System.Threading.Tasks;
using AetherlinkPriceServer.Options;
using AetherlinkPriceServer.Provider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherlinkPriceServer.Worker;

public class HourlyPriceWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly HourlyPriceOption _option;
    private readonly IPriceProvider _priceProvider;
    private readonly ILogger<HourlyPriceWorker> _logger;

    public HourlyPriceWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        ILogger<HourlyPriceWorker> logger, IPriceProvider priceProvider,
        IOptionsSnapshot<HourlyPriceOption> option) : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _option = option.Value;
        _logger.LogDebug($"get MetricsReportOption: {JsonConvert.SerializeObject(option.Value)}");
        _priceProvider = priceProvider;
        SetNextWholeHourTimer();
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.LogInformation("HourlyPriceWorker ...");

        var time = DateTime.Now;

        await Task.WhenAll((await _priceProvider.GetPriceListAsync(_option.Tokens)).Select(p =>
        {
            p.UpdateTime = new DateTime(time.Year, time.Month, time.Day, time.Hour, 0, 0);
            return _priceProvider.UpdateHourlyPriceAsync(p);
        }));

        SetNextWholeHourTimer();
    }

    private void SetNextWholeHourTimer()
    {
        var now = DateTime.Now;
        var nextHour = now.Hour == 23 ? now.Date.AddDays(1) : now.AddHours(1).Date.AddHours(now.Hour + 1);
        var period = (int)(nextHour - now).TotalMilliseconds + 10000;
        _logger.LogInformation(
            $"Next whole hour time: {nextHour}, The millisecond time interval to the next whole hour {period}");
        Timer.Period = period;
    }
}