using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using Volo.Abp.DependencyInjection;
using Nethereum.Web3.Accounts;
using Nethereum.Web3.Accounts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AetherLink.Worker.Core.Provider;

public interface IEvmContractProvider
{
}

public class EvmContractProvider : IEvmContractProvider, ISingletonDependency
{
    private readonly ILogger<EvmContractProvider> _logger;

    public EvmContractProvider(ILogger<EvmContractProvider> logger)
    {
        _logger = logger;
    }

    // public async Task<string> TransmitAsync(string chainId, string contractAddress, byte[] swapHashId, byte[] report,
    //     byte[][] rs, byte[][] ss, byte[] rawVs)
    // {
    //     var setValueFunction = GetFunction(chainId, contractAddress, "transmit");
    //     var sender = GetAccount().Address;
    //
    //     _logger.LogInformation($"Transmit sender: {sender}");
    //
    //     var gas = await setValueFunction.EstimateGasAsync(sender, null, null, swapHashId, report, rs, ss, rawVs);
    //     gas.Value = BigInteger.Multiply(gas.Value, 2);
    //     _logger.LogInformation($"Transmit params: report:{report.ToHex()},rawVs:{rawVs.ToHex()}");
    //     foreach (var r in rs)
    //     {
    //         _logger.LogInformation($"Transmit params: rs:{r.ToHex()}");
    //     }
    //
    //     foreach (var s in ss)
    //     {
    //         _logger.LogInformation($"Transmit params: rs:{s.ToHex()}");
    //     }
    //
    //     var transactionResult =
    //         await setValueFunction.SendTransactionAsync(sender, gas, null, null, swapHashId, report,
    //             rs, ss, rawVs);
    //     return transactionResult;
    // }

    // private Function GetFunction(string chainId, string contractAddress, string methodName)
    // {
    //     var clientAlias = EthereumAElfChainAliasOptions.Value.Mapping[chainId];
    //     var accountAlias = EthereumClientConfigOptions.Value.AccountAlias;
    //     var client = NethereumClientProvider.GetClient(clientAlias, accountAlias);
    //     client.TransactionManager.UseLegacyAsDefault = true;
    //     var contract = client.Eth.GetContract(GetAbi(), contractAddress);
    //     return contract.GetFunction(methodName);
    // }

    private Account GetAccount()
    {
        return new Account("private key");
    }
    
    // private string GetAbi()
    // {
    //     var path = Path.Combine(EthereumContractOptions.Value.AbiFileDirectory,
    //         EthereumContractOptions.Value.ContractInfoList[SmartContractName].AbiFileName);
    //     
    //     using var file = System.IO.File.OpenText(path);
    //     using var reader = new JsonTextReader(file);
    //     var o = (JObject) JToken.ReadFrom(reader);
    //     var value = o["abi"]?.ToString();
    //     return value;
    // }
}