using System.Threading.Tasks;
using Aetherlink.PriceServer;
using Aetherlink.PriceServer.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherLink.Worker.Core.Worker;

public class PriceWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly IPriceServerProvider _serverProvider;
    private readonly ILogger<PriceWorker> _logger;

    public PriceWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IPriceServerProvider serverProvider, ILogger<PriceWorker> logger) : base(timer, serviceScopeFactory)
    {
        _serverProvider = serverProvider;
        _logger = logger;
        Timer.Period = 1000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var dailyResult = await _serverProvider.GetDailyPriceAsync(new()
        {
            TokenPair = "elf-usd",
            TimeStamp = "20240813"
        });

        if (dailyResult.Data.Price > 0)
        {
            _logger.LogInformation("get daily price succeed.");
        }
        else
        {
            _logger.LogError("get daily price succeed.");
        }

        var aggregatedResult = await _serverProvider.GetAggregatedTokenPriceAsync(new()
        {
            TokenPair = "elf-usd",
            AggregateType = AggregateType.Latest
        });

        if (dailyResult.Data.Price > 0)
        {
            _logger.LogInformation("get aggregated price succeed.");
        }
        else
        {
            _logger.LogError("get aggregated price succeed.");
        }
    }
}