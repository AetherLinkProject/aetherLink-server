using System;
using System.IO;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Options;
using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Volo.Abp.DependencyInjection;
using BigInteger = System.Numerics.BigInteger;

namespace AetherLink.Worker.Core.Provider;

public interface IEvmProvider
{
    Task<string> TransmitAsync(EvmOptions evmOptions, byte[] contextBytes, byte[] messageBytes, byte[] tokenAmountBytes,
        byte[][] rs, byte[][] ss, byte[] rawVs);
}

public class EvmProvider : IEvmProvider, ISingletonDependency
{
    private readonly ILogger<EvmProvider> _logger;

    public EvmProvider(ILogger<EvmProvider> logger)
    {
        _logger = logger;
    }

    public async Task<string> TransmitAsync(EvmOptions evmOptions, byte[] contextBytes, byte[] messageBytes,
        byte[] tokenAmountBytes, byte[][] rs, byte[][] ss, byte[] rawVs)
    {
        try
        {
            _logger.LogInformation("Starting evm transaction preparation...");

            var function = GetTransmitFunction(evmOptions);
            var account = GetWeb3Account(evmOptions);
            // var gasLimit = new Nethereum.Hex.HexTypes.HexBigInteger(300000);
            var gas = await function.EstimateGasAsync(
                from: account.TransactionManager.Account.Address,
                null,
                null,
                contextBytes,
                messageBytes,
                tokenAmountBytes,
                rs,
                ss,
                rawVs);
            gas.Value = BigInteger.Multiply(gas.Value, 2);
            var transactionHash = await function.SendTransactionAsync(
                from: account.TransactionManager.Account.Address,
                gas: gas,
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

    private Function GetTransmitFunction(EvmOptions evmOptions)
    {
        try
        {
            _logger.LogInformation("Loading contract ABI...");

            var abiContent = File.OpenText(EvmTransactionConstants.AbiFileName);
            var abiObject = (JObject)JToken.ReadFrom(new JsonTextReader(abiContent));
            var abiJson = abiObject?["abi"]?.ToString() ??
                          throw new InvalidOperationException("ABI is missing in the file.");
            var contract = GetWeb3Account(evmOptions).Eth.GetContract(abiJson, evmOptions.ContractAddress);
            return contract.GetFunction(EvmTransactionConstants.TransmitMethodName);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load contract ABI or get function: {ex.Message}", ex);
            throw;
        }
    }

    private Web3 GetWeb3Account(EvmOptions evmOptions)
    {
        var account = new Account(evmOptions.TransmitterSecret);
        return new Web3(account, evmOptions.Api);
    }
}