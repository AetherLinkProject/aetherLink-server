using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Options;
using AetherLink.Server.HttpApi.Options;
using AetherLink.Server.HttpApi.Constants;

public abstract class EvmBaseBalanceProvider : ChainBalanceProvider
{
    protected readonly HttpClient HttpClient;
    protected readonly string ChainName;
    protected readonly IOptionsSnapshot<BalanceMonitorOptions> Options;

    protected EvmBaseBalanceProvider(HttpClient httpClient, IOptionsSnapshot<BalanceMonitorOptions> options,
        string chainName)
    {
        HttpClient = httpClient;
        Options = options;
        ChainName = chainName;
    }

    protected string Url => Options.Value.Chains[ChainName].Url;
    protected string ApiKey => Options.Value.Chains[ChainName].ApiKey;

    public override async Task<decimal> GetBalanceAsync(string address)
    {
        var rpcRequest = new
        {
            jsonrpc = "2.0",
            method = "eth_getBalance",
            @params = new[] { address, "latest" },
            id = 1
        };
        var request = new HttpRequestMessage(HttpMethod.Post, Url)
        {
            Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(rpcRequest),
                System.Text.Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(ApiKey))
            request.Headers.Add("X-Api-Key", ApiKey);
        var response = await HttpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(content);
        var resultStr = json["result"]?.ToString() ?? "0";
        System.Numerics.BigInteger balanceWei;
        balanceWei = resultStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? System.Numerics.BigInteger.Parse(resultStr.Substring(2), System.Globalization.NumberStyles.HexNumber)
            : System.Numerics.BigInteger.Parse(resultStr);
        var balance = (decimal)balanceWei / 1_000_000_000_000_000_000m;
        return balance;
    }
}

public class EthBalanceProvider : EvmBaseBalanceProvider
{
    public EthBalanceProvider(HttpClient httpClient, IOptionsSnapshot<BalanceMonitorOptions> options)
        : base(httpClient, options, ChainNamesConstants.Eth)
    {
    }

    public override string ChainType => ChainTypesConstants.Eth;
}

public class SepoliaBalanceProvider : EvmBaseBalanceProvider
{
    public SepoliaBalanceProvider(HttpClient httpClient, IOptionsSnapshot<BalanceMonitorOptions> options)
        : base(httpClient, options, ChainNamesConstants.Sepolia)
    {
    }

    public override string ChainType => ChainTypesConstants.Sepolia;
}

public class BscBalanceProvider : EvmBaseBalanceProvider
{
    public BscBalanceProvider(HttpClient httpClient, IOptionsSnapshot<BalanceMonitorOptions> options)
        : base(httpClient, options, ChainNamesConstants.Bsc)
    {
    }

    public override string ChainType => ChainTypesConstants.Bsc;
}

public class BscTestBalanceProvider : EvmBaseBalanceProvider
{
    public BscTestBalanceProvider(HttpClient httpClient, IOptionsSnapshot<BalanceMonitorOptions> options)
        : base(httpClient, options, ChainNamesConstants.BscTest)
    {
    }

    public override string ChainType => ChainTypesConstants.BscTest;
}

public class BaseBalanceProvider : EvmBaseBalanceProvider
{
    public BaseBalanceProvider(HttpClient httpClient, IOptionsSnapshot<BalanceMonitorOptions> options)
        : base(httpClient, options, ChainNamesConstants.Base)
    {
    }

    public override string ChainType => ChainTypesConstants.Base;
}

public class BaseSepoliaBalanceProvider : EvmBaseBalanceProvider
{
    public BaseSepoliaBalanceProvider(HttpClient httpClient, IOptionsSnapshot<BalanceMonitorOptions> options)
        : base(httpClient, options, ChainNamesConstants.BaseSepolia)
    {
    }

    public override string ChainType => ChainTypesConstants.BaseSepolia;
}