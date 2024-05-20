using System;
using System.Linq;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Options;
using AetherlinkPriceServer.Provider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        _priceProvider = priceProvider;
        SetNextWholeHourTimer();
    }


    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.LogInformation("HourlyPriceWorker ...");

        await Task.WhenAll((await _priceProvider.GetPriceListAsync(_option.Tokens)).Select(p =>
            _priceProvider.UpdateHourlyPriceAsync(p)));

        SetNextWholeHourTimer();
    }

    private void SetNextWholeHourTimer()
    {
        var now = DateTime.Now;
        var nextHour = now.Hour == 0 ? now.Date.AddHours(1) : now.AddHours(1).Date.AddHours(now.Hour + 1);
        var period = (int)(nextHour - now).TotalMilliseconds + 10000;
        _logger.LogInformation(
            $"Next whole hour time: {nextHour}, The millisecond time interval to the next whole hour {period}");
        Timer.Period = period;
    }
}