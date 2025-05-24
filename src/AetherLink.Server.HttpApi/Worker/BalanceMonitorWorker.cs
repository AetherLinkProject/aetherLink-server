using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using AetherLink.Server.HttpApi.Options;
using AetherLink.Server.HttpApi.Reporter;
using AetherLink.Server.HttpApi.Constants;

public class BalanceMonitorWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly BalanceMonitorOptions _options;
    private readonly BalanceReporter _balanceReporter;
    private readonly ILogger<BalanceMonitorWorker> _logger;
    private readonly Dictionary<string, IChainBalanceProvider> _providers;

    public BalanceMonitorWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<BalanceMonitorOptions> options, IEnumerable<IChainBalanceProvider> providers,
        BalanceReporter balanceReporter, ILogger<BalanceMonitorWorker> logger) : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        timer.Period = _options.Period;
        _balanceReporter = balanceReporter;
        _providers = providers.ToDictionary(
            p => p.ChainKey.ToLower(),
            p => p
        );
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var chains = _options.Chains;
        if (chains == null || chains.Count == 0)
        {
            _logger.LogInformation("No chains configured for balance monitoring.");
            return;
        }

        // Only process chains that are both configured and have a registered provider
        var validChains = chains.Where(c => _providers.ContainsKey(c.Key.ToLower()))
            .Select(c => new { ChainName = c.Key, Addresses = c.Value.Addresses })
            .ToList();
        if (validChains.Count == 0)
        {
            _logger.LogWarning("No configured chains have a registered provider.");
            return;
        }

        _logger.LogInformation(
            $"BalanceMonitorWorker starting. Monitoring chains: {string.Join(", ", validChains.Select(c => $"{c.ChainName}({c.Addresses.Count})"))}");
        var chainTasks =
            validChains.Select(chain => Task.Run(() => ProcessChainAsync(chain.ChainName, chain.Addresses)));
        await Task.WhenAll(chainTasks);
    }

    private async Task ProcessChainAsync(string chainName, List<string> addresses)
    {
        if (!_providers.TryGetValue(chainName.ToLower(), out var provider))
        {
            _logger.LogWarning($"No provider found for chain: {chainName}");
            return;
        }

        foreach (var address in addresses)
        {
            int retryCount = 0;
            while (retryCount < MetricsConstants.MaxRetries)
            {
                try
                {
                    var balance = await provider.GetBalanceAsync(address);
                    _balanceReporter.SetBalance(chainName, address, balance);
                    _logger.LogInformation($"[{chainName.ToUpper()}] Balance for {address}: {balance}");
                    break;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    _logger.LogWarning(ex,
                        $"Failed to get {chainName} balance for {address}, attempt {retryCount}/{MetricsConstants.MaxRetries}");
                    if (retryCount >= MetricsConstants.MaxRetries)
                    {
                        _logger.LogError(
                            $"[{chainName.ToUpper()}] Giving up on {address} after {MetricsConstants.MaxRetries} attempts.");
                    }
                    else
                    {
                        await Task.Delay(MetricsConstants.RetryDelayMs);
                    }
                }
            }

            await Task.Delay(MetricsConstants.AddressDelayMs);
        }
    }
}