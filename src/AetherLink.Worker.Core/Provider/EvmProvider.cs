using System;
using System.IO;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IEvmProvider
{
    Task<string> TransmitAsync(byte[] contextBytes, byte[] messageBytes, byte[] tokenAmountBytes, byte[][] rs,
        byte[][] ss, byte[] rawVs);
}

public class EvmProvider : IEvmProvider, ISingletonDependency
{
    private readonly EvmOptions _evmOptions;
    private readonly ChainConfig _chainConfig;
    private readonly ILogger<EvmProvider> _logger;

    public EvmProvider(IOptionsSnapshot<EvmOptions> evmConfig, ILogger<EvmProvider> logger,
        IOptionsSnapshot<ChainConfig> chainConfig)
    {
        _logger = logger;
        _evmOptions = evmConfig.Value;
        _chainConfig = chainConfig.Value;
    }

    public async Task<string> TransmitAsync(byte[] contextBytes, byte[] messageBytes, byte[] tokenAmountBytes,
        byte[][] rs, byte[][] ss, byte[] rawVs)
    {
        try
        {
            _logger.LogInformation("Starting evm transaction preparation...");

            var function = GetTransmitFunction();
            var gasLimit = new Nethereum.Hex.HexTypes.HexBigInteger(300000);
            var transactionHash = await function.SendTransactionAsync(
                from: GetWeb3Account().TransactionManager.Account.Address,
                gas: gasLimit,
                null,
                null,
                contextBytes,
                messageBytes,
                tokenAmountBytes,
                rs,
                ss,
                rawVs
            );

            _logger.LogInformation($"[Evm] Transaction successful! Hash: {transactionHash}");
            return transactionHash;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Transaction failed: {ex.Message}", ex);
            throw;
        }
    }

    private Function GetTransmitFunction()
    {
        try
        {
            _logger.LogInformation("Loading contract ABI...");

            var abiContent = File.OpenText(Path.Combine("ContractBuild", "RampAbi.json"));
            var abiObject = (JObject)JToken.ReadFrom(new JsonTextReader(abiContent));
            var abiJson = abiObject?["abi"]?.ToString() ??
                          throw new InvalidOperationException("ABI is missing in the file.");
            var contract = GetWeb3Account().Eth.GetContract(abiJson, _evmOptions.ContractAddress);
            return contract.GetFunction(EvmTransactionConstants.TransmitMethodName);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load contract ABI or get function: {ex.Message}", ex);
            throw;
        }
    }

    private Web3 GetWeb3Account()
    {
        // var account = new Account(_chainConfig.TransmitterSecret);
        var account = new Account("80533a7d5ed1b7a128d1ba28fdfe280641adc5e3306b5007a2f7acfbececee3d");
        return new Web3(account, _evmOptions.Url);
    }
}