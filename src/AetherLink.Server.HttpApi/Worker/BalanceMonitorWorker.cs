using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using AetherLink.Server.HttpApi.Options;
using AetherLink.Server.HttpApi.Reporter;

public class BalanceMonitorWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly BalanceMonitorOptions _options;
    private readonly ILogger<BalanceMonitorWorker> _logger;
    private readonly IBalanceMonitorProvider _balanceMonitorProvider;
    private readonly BalanceReporter _balanceReporter;

    public BalanceMonitorWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<BalanceMonitorOptions> options,
        IBalanceMonitorProvider balanceMonitorProvider,
        BalanceReporter balanceReporter,
        ILogger<BalanceMonitorWorker> logger
    ) : base(timer, serviceScopeFactory)
    {
        _options = options.Value;
        _balanceMonitorProvider = balanceMonitorProvider;
        _balanceReporter = balanceReporter;
        _logger = logger;
        timer.Period = 60000; // 1 minute, configurable
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var chains = _options.Chains;
        if (chains == null || chains.Count == 0)
        {
            _logger.LogInformation("No chains configured for balance monitoring.");
            return;
        }
        foreach (var chain in chains)
        {
            var chainName = chain.Key;
            var addresses = chain.Value.Addresses;
            foreach (var address in addresses)
            {
                try
                {
                    var balance = await _balanceMonitorProvider.GetBalanceAsync(chainName, address);
                    _balanceReporter.SetBalance(chainName, address, balance);
                    _logger.LogInformation($"[{chainName.ToUpper()}] Balance for {address}: {balance}");
                    await Task.Delay(1000); // 1 second delay between requests to avoid rate limit
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to get {chainName} balance for {address}");
                }
            }
        }
    }
} 