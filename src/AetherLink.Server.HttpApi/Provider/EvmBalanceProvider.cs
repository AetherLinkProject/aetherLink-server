using Microsoft.Extensions.Options;
using AetherLink.Server.HttpApi.Options;
using AetherLink.Server.HttpApi.Constants;
using Nethereum.Web3;
using Nethereum.Util;

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
        var web3 = new Web3(Url);
        var checksumAddress = AddressUtil.Current.ConvertToChecksumAddress(address);
        var balanceWei = await web3.Eth.GetBalance.SendRequestAsync(checksumAddress);
        return Web3.Convert.FromWei(balanceWei);
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