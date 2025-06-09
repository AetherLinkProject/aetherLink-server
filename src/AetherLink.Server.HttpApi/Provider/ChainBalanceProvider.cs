using Microsoft.Extensions.Options;
using AetherLink.Server.HttpApi.Options;

public interface IChainBalanceProvider
{
    Task<decimal> GetBalanceAsync(string address);
    string ChainName { get; }
    string ChainType { get; }
}

public abstract class ChainBalanceProvider : IChainBalanceProvider
{
    private readonly string _chainName;
    protected readonly HttpClient HttpClient;
    private readonly IOptionsSnapshot<BalanceMonitorOptions> _options;

    protected ChainBalanceProvider(HttpClient httpClient, IOptionsSnapshot<BalanceMonitorOptions> options,
        string chainName)
    {
        _options = options;
        _chainName = chainName;
        HttpClient = httpClient;
    }

    protected string Url => _options.Value.Chains[_chainName].Url;
    protected string ApiKey => _options.Value.Chains[_chainName].ApiKey;

    public abstract Task<decimal> GetBalanceAsync(string address);
    public string ChainName => _chainName;
    public abstract string ChainType { get; }
}