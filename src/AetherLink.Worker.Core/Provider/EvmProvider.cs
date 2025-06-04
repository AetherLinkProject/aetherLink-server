using System;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using AetherLink.Worker.Core.ChainHandler;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Options;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IEvmProvider
{
    Task<string> TransmitAsync(EvmOptions evmOptions, byte[] contextBytes, byte[] messageBytes,
        byte[] tokenTransferMetadataBytes, byte[][] signatures);

    Task<TransactionState> GetTransactionResultAsync(EvmOptions evmOptions, string transactionId);
}

public class EvmProvider : IEvmProvider, ISingletonDependency
{
    private readonly ILogger<EvmProvider> _logger;

    public EvmProvider(ILogger<EvmProvider> logger)
    {
        _logger = logger;
    }

    public async Task<string> TransmitAsync(EvmOptions evmOptions, byte[] contextBytes, byte[] messageBytes,
        byte[] tokenTransferMetadataBytes, byte[][] signatures)
    {
        try
        {
            _logger.LogInformation("Starting evm transaction preparation...");

            var function = GetTransmitFunction(evmOptions);
            var account = GetWeb3Account(evmOptions);
            var gasPrice = await account.Eth.GasPrice.SendRequestAsync();
            var minGasPrice = Web3.Convert.ToWei(evmOptions.MinGasPrice, Nethereum.Util.UnitConversion.EthUnit.Gwei);
            if (gasPrice.Value < minGasPrice) gasPrice = new HexBigInteger(minGasPrice);

            _logger.LogDebug(
                $"[Evm] Current Gas Price: {Web3.Convert.FromWei(gasPrice, Nethereum.Util.UnitConversion.EthUnit.Gwei)} Gwei");

            var gas = await function.EstimateGasAsync(
                from: account.TransactionManager.Account.Address,
                gasPrice,
                null,
                contextBytes,
                messageBytes,
                tokenTransferMetadataBytes,
                signatures
            );
            gas.Value = BigInteger.Multiply(gas.Value, 2);
            _logger.LogDebug($"[Evm] Estimate transmit gas result: {gas.ToUlong()}");

            var transactionHash = await function.SendTransactionAsync(
                account.TransactionManager.Account.Address,
                gas,
                gasPrice,
                null,
                contextBytes,
                messageBytes,
                tokenTransferMetadataBytes,
                signatures
            );
            _logger.LogInformation($"[Evm] Transaction successful! Hash: {transactionHash}");

            return transactionHash;
        }
        catch (Nethereum.JsonRpc.Client.RpcResponseException re)
        {
            _logger.LogError(re, $"[Evm] Transaction is revert: {re.Message}");

            return string.Empty;
        }
        catch (Nethereum.ABI.FunctionEncoding.SmartContractRevertException se)
        {
            _logger.LogError(se, $"[Evm] Transaction is revert by smart contract: {se.Message}");
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[Evm] Transaction failed: {ex.Message}", ex);
            throw;
        }
    }

    public async Task<TransactionState> GetTransactionResultAsync(EvmOptions evmOptions, string transactionId)
    {
        var account = GetWeb3Account(evmOptions);

        for (var i = 0; i < RetryConstants.MaximumRetryTimes; i++)
        {
            try
            {
                var receipt = await account.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionId);
                if (receipt != null && receipt.Status != null && receipt.Status == new HexBigInteger(1))
                    return TransactionState.Success;
            }
            catch (Exception ex)
            {
                if (ex.Message != null && ex.Message.Contains("Duplicate report: already processed"))
                {
                    _logger.LogWarning($"[Evm] {transactionId} duplicate report detected, treat as success.");
                    return TransactionState.Success;
                }
                _logger.LogWarning($"[Evm] {transactionId} exception in {i} times: {ex.Message}");
            }

            _logger.LogWarning(
                $"[Evm][Leader] {transactionId} send transaction failed in {i} times, will send it later.");

            await Task.Delay((i + 1) * 1000 * 2);
        }

        return TransactionState.Fail;
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