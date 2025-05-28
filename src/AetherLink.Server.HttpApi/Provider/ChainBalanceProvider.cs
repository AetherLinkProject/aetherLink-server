using Microsoft.Extensions.Options;
using AetherLink.Server.HttpApi.Options;

public interface IChainBalanceProvider
{
    Task<decimal> GetBalanceAsync(string address);
    string ChainKey { get; }
}

public abstract class ChainBalanceProvider : IChainBalanceProvider
{
    private readonly string _chainKey;
    protected readonly HttpClient HttpClient;
    private readonly IOptionsSnapshot<BalanceMonitorOptions> _options;

    protected ChainBalanceProvider(HttpClient httpClient, IOptionsSnapshot<BalanceMonitorOptions> options,
        string chainKey)
    {
        _options = options;
        _chainKey = chainKey;
        HttpClient = httpClient;
    }

    protected string Url => _options.Value.Chains[_chainKey].Url;
    protected string ApiKey => _options.Value.Chains[_chainKey].ApiKey;

    public abstract Task<decimal> GetBalanceAsync(string address);
    public string ChainKey => _chainKey;
}