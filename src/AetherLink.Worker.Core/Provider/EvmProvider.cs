using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common.ContractHandler;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using Nethereum.Signer;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IEvmProvider
{
    Task<string> TransmitAsync(ReportContextDto reportContext, CrossChainDataDto report, EvmSignatureDto signature);
}

public class EvmProvider : IEvmProvider, ISingletonDependency
{
    private readonly Web3 _web3;
    private readonly EvmOptions _evmOptions;
    private readonly ChainConfig _chainConfig;
    private readonly ContractOptions _options;
    private readonly ILogger<EvmProvider> _logger;

    public EvmProvider(IOptionsSnapshot<ContractOptions> options, IOptionsSnapshot<ChainConfig> chainConfig,
        IOptionsSnapshot<EvmOptions> evmConfig, ILogger<EvmProvider> logger)
    {
        _logger = logger;
        _options = options.Value;
        _evmOptions = evmConfig.Value;
        _chainConfig = chainConfig.Value;

        var account = new Account(_chainConfig.TransmitterSecret);
        _web3 = new Web3(account, $"{_evmOptions.Url}{_evmOptions.ApiKey}");
    }

    // public async Task<string> TransmitAsync(ReportContextDto reportContext, CrossChainDataDto report,
    //     EvmSignatureDto signature)
    // {
    //     var account = new Account(_chainConfig.TransmitterSecret);
    //     var web3 = new Web3(account, _evmOptions.Url + _evmOptions.ApiKey);
    //     var o = (JObject)JToken.ReadFrom(new JsonTextReader(File.OpenText(Path.Combine(
    //         EvmTransactionConstants.ContractFilePath, EvmTransactionConstants.AbiFileName))));
    //     var transmitFunction = web3.Eth
    //         .GetContract(o[EvmTransactionConstants.AbiAliasName]?.ToString(), _evmOptions.ContractAddress)
    //         .GetFunction(EvmTransactionConstants.TransmitMethodName);
    //
    //     var reportContextTuple = new object[]
    //     {
    //         reportContext.MessageId,
    //         new BigInteger(reportContext.SourceChainId),
    //         new BigInteger(reportContext.TargetChainId),
    //         reportContext.Sender,
    //         reportContext.Receiver
    //     };
    //
    //     var tokenAmount = report.TokenAmount;
    //     var tokenAmountTuple = new object[]
    //     {
    //         tokenAmount.SwapId,
    //         new BigInteger(tokenAmount.TargetChainId),
    //         tokenAmount.TargetContractAddress,
    //         tokenAmount.TokenAddress,
    //         tokenAmount.OriginToken,
    //         new BigInteger(tokenAmount.Amount)
    //     };
    //
    //     // var gasPrice = Web3.Convert.ToWei(10, Nethereum.Util.UnitConversion.EthUnit.Gwei);
    //     var gasLimit = new Nethereum.Hex.HexTypes.HexBigInteger(300000);
    //
    //     return await transmitFunction.SendTransactionAsync(
    //         new EthECKey(_chainConfig.TransmitterSecret).GetPublicAddress(),
    //         gasLimit,
    //         null,
    //         reportContextTuple,
    //         report.Message,
    //         tokenAmountTuple,
    //         signature.R,
    //         signature.S,
    //         signature.V
    //     );
    // }

    // private Function GetFunction(string contractAddress, string methodName)
    // {
    //     // var infuraUrl = "https://mainnet.infura.io/v3/YOUR-PROJECT-ID";
    //     // var privateKey = "YOUR-WALLET-PRIVATE-KEY";
    //     var account = new Account(_chainConfig.TransmitterSecret);
    //     var web3 = new Web3(account, _evmOptions.Url + _evmOptions.ApiKey);
    //     var o = (JObject)JToken.ReadFrom(
    //         new JsonTextReader(File.OpenText(Path.Combine("ContractBuild", "RampAbi.json"))));
    //     return web3.Eth.GetContract(o["abi"]?.ToString(), contractAddress).GetFunction(methodName);
    // }
    //
    // private Account GetAccount(string secret) => new Account(secret);

    public async Task<string> TransmitAsync(ReportContextDto reportContext, CrossChainDataDto report,
        EvmSignatureDto signature)
    {
        try
        {
            _logger.LogInformation("Starting transaction preparation...");

            var reportContextTuple = BuildReportContextTuple(reportContext);
            var tokenAmountTuple = BuildTokenAmountTuple(report.TokenAmount);
            var function = GetTransmitFunction();
            var transactionHash = await SendTransactionAsync(function, reportContextTuple, report.Message,
                tokenAmountTuple, signature);

            _logger.LogInformation($"Transaction successful! Hash: {transactionHash}");
            return transactionHash;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Transaction failed: {ex.Message}", ex);
            throw;
        }
    }

    private object[] BuildReportContextTuple(ReportContextDto reportContext)
    {
        return new object[]
        {
            reportContext.MessageId,
            new BigInteger(reportContext.SourceChainId),
            new BigInteger(reportContext.TargetChainId),
            reportContext.Sender,
            reportContext.Receiver
        };
    }

    private object[] BuildTokenAmountTuple(TokenAmountDto tokenAmount)
    {
        return new object[]
        {
            tokenAmount.SwapId,
            new BigInteger(tokenAmount.TargetChainId),
            tokenAmount.TargetContractAddress,
            tokenAmount.TokenAddress,
            tokenAmount.OriginToken,
            new BigInteger(tokenAmount.Amount)
        };
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
            var contract = _web3.Eth.GetContract(abiJson, _evmOptions.ContractAddress);
            return contract.GetFunction(EvmTransactionConstants.TransmitMethodName);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load contract ABI or get function: {ex.Message}", ex);
            throw;
        }
    }

    private async Task<string> SendTransactionAsync(Function function, object[] reportContextTuple, string message,
        object[] tokenAmountTuple, EvmSignatureDto signature)
    {
        try
        {
            // var gasLimit = await function.EstimateGasAsync(
            //     senderAddress: _web3.TransactionManager.Account.Address, 
            //     gas: null,
            //     value: null,
            //     reportContextTuple,
            //     message,
            //     tokenAmountTuple,
            //     signature.R,
            //     signature.S,
            //     signature.V
            // );
            _logger.LogInformation("Estimating gas limit...");
            var gasLimit = new Nethereum.Hex.HexTypes.HexBigInteger(300000);
            
            // _logger.LogInformation("Estimating gas price...");
            // var gasPrice = await _web3.Eth.GasPrice.SendRequestAsync();

            _logger.LogInformation("Sending transaction...");
            return await function.SendTransactionAsync(
                from: _web3.TransactionManager.Account.Address, 
                gas: gasLimit,
                // gasPrice: gasPrice,
                null,
                reportContextTuple,
                message,
                tokenAmountTuple,
                signature.R,
                signature.S,
                signature.V
            );
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error while sending transaction: {ex.Message}");
            throw;
        }
    }
}