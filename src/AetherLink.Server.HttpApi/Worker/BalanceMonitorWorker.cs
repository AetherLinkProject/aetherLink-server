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
    private readonly Dictionary<string, IChainBalanceProvider> _providerByType;

    public BalanceMonitorWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<BalanceMonitorOptions> options, IEnumerable<IChainBalanceProvider> providers,
        BalanceReporter balanceReporter, ILogger<BalanceMonitorWorker> logger) : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        timer.Period = _options.Period;
        _balanceReporter = balanceReporter;
        _providerByType = providers.ToDictionary(p => p.ChainType.ToLower(), p => p);
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.LogInformation("[BalanceMonitorWorker] Starting.");

        var chains = _options.Chains;
        if (chains == null || chains.Count == 0)
        {
            _logger.LogInformation("[BalanceMonitorWorker] No chains configured for balance monitoring.");
            return;
        }

        var chainTasks =
            chains.Select(chain => Task.Run(() => ProcessChainAsync(chain.Key, chain.Value)));
        await Task.WhenAll(chainTasks);
    }

    private async Task ProcessChainAsync(string chainName, ChainBalanceMonitorOptions chainOptions)
    {
        var addresses = chainOptions.Addresses;
        var chainType = chainOptions.ChainType?.ToLower();
        if (string.IsNullOrEmpty(chainType) || !_providerByType.TryGetValue(chainType, out var provider))
        {
            _logger.LogWarning(
                $"[BalanceMonitorWorker] No provider found for chainType: {chainType} (chain: {chainName})");
            return;
        }

        var balanceDict = new Dictionary<string, decimal>();
        foreach (var address in addresses)
        {
            var retryCount = 0;
            while (retryCount < MetricsConstants.MaxRetries)
            {
                try
                {
                    var balance = await provider.GetBalanceAsync(address);
                    _balanceReporter.SetBalance(chainName, address, balance);
                    balanceDict[address] = balance;
                    break;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    _logger.LogWarning(ex,
                        $"[BalanceMonitorWorker] Failed to get {chainName} balance for {address}, attempt {retryCount}/{MetricsConstants.MaxRetries}");
                    if (retryCount >= MetricsConstants.MaxRetries)
                    {
                        _logger.LogError(
                            $"[BalanceMonitorWorker][{chainName.ToUpper()}] Giving up on {address} after {MetricsConstants.MaxRetries} attempts.");
                    }
                    else
                    {
                        await Task.Delay(MetricsConstants.RetryDelayMs);
                    }
                }
            }

            await Task.Delay(MetricsConstants.AddressDelayMs);
        }

        if (balanceDict.Count > 0)
        {
            var summary = string.Join(", ", balanceDict.Select(kv => $"{kv.Key}:{kv.Value}"));
            _logger.LogInformation($"[BalanceMonitorWorker][{chainName.ToUpper()}] Balances: {{ {summary} }}");
        }
    }
}