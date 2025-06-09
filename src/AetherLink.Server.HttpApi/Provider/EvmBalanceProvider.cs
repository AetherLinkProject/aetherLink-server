using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Options;
using AetherLink.Server.HttpApi.Options;
using AetherLink.Server.HttpApi.Constants;

public class EvmBalanceProvider : ChainBalanceProvider
{
    public override string ChainType => ChainTypesConstants.Evm;

    public EvmBalanceProvider(HttpClient httpClient, IOptionsSnapshot<BalanceMonitorOptions> options,
        string chainName = ChainNamesConstants.Eth)
        : base(httpClient, options, chainName)
    {
    }

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
        var resultHex = json["result"]?.ToString() ?? "0x0";
        var balanceWei =
            System.Numerics.BigInteger.Parse(resultHex.Substring(2), System.Globalization.NumberStyles.HexNumber);
        var balance = (decimal)balanceWei / 1_000_000_000_000_000_000m;
        return balance;
    }
}

public class EthBalanceProvider : EvmBalanceProvider
{
    public EthBalanceProvider(HttpClient httpClient, IOptionsSnapshot<BalanceMonitorOptions> options)
        : base(httpClient, options, ChainNamesConstants.Eth)
    {
    }
}

public class SepoliaBalanceProvider : EvmBalanceProvider
{
    public SepoliaBalanceProvider(HttpClient httpClient, IOptionsSnapshot<BalanceMonitorOptions> options)
        : base(httpClient, options, "sepolia")
    {
    }
}

public class BscBalanceProvider : EvmBalanceProvider
{
    public BscBalanceProvider(HttpClient httpClient, IOptionsSnapshot<BalanceMonitorOptions> options)
        : base(httpClient, options, ChainConstants.Bsc)
    {
    }
}

public class BscTestBalanceProvider : EvmBalanceProvider
{
    public BscTestBalanceProvider(HttpClient httpClient, IOptionsSnapshot<BalanceMonitorOptions> options)
        : base(httpClient, options, "bsctest")
    {
    }
}

public class BaseBalanceProvider : EvmBalanceProvider
{
    public BaseBalanceProvider(HttpClient httpClient, IOptionsSnapshot<BalanceMonitorOptions> options)
        : base(httpClient, options, ChainConstants.Base)
    {
    }
}

public class BaseSepoliaBalanceProvider : EvmBalanceProvider
{
    public BaseSepoliaBalanceProvider(HttpClient httpClient, IOptionsSnapshot<BalanceMonitorOptions> options)
        : base(httpClient, options, "basesepolia")
    {
    }
}