using System;
using System.Threading.Tasks;
using System.Collections.Generic;

public interface IBalanceMonitorProvider
{
    Task<decimal> GetBalanceAsync(string chain, string address);
}

public interface IChainBalanceProvider
{
    Task<decimal> GetBalanceAsync(string address);
}

public class BalanceMonitorProvider : IBalanceMonitorProvider
{
    private readonly Dictionary<string, IChainBalanceProvider> _providers;

    public BalanceMonitorProvider(Dictionary<string, IChainBalanceProvider> providers)
    {
        _providers = providers;
    }

    public async Task<decimal> GetBalanceAsync(string chain, string address)
    {
        if (_providers.TryGetValue(chain.ToLower(), out var provider))
        {
            return await provider.GetBalanceAsync(address);
        }
        throw new NotSupportedException($"Chain {chain} is not supported.");
    }
} 