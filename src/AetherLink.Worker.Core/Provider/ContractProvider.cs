using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Client.Dto;
using AElf.Client.Service;
using AElf.Types;
using AetherLink.Contracts.Oracle;
using AetherLink.Worker.Core.Common.ContractHandler;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Options;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Options;
using Oracle;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IContractProvider
{
    public Task<Hash> GetRandomHashAsync(long blockNumber, string chainId);
    public Task<GetConfigOutput> GetOracleConfigAsync(string chainId);
    public Task<Int64Value> GetLatestRoundAsync(string chainId);
    public Task<string> SendTransmitAsync(string chainId, TransmitInput transmitInput);
    public Task<long> GetBlockLatestHeightAsync(string chainId);
    public Task<Commitment> GetCommitmentAsync(string chainId, string transactionId);
    public Task<TransactionResultDto> GetTxResultAsync(string chainId, string transactionId);
    public Transmitted ParseTransmitted(TransactionResultDto transaction);
}

public class ContractProvider : IContractProvider, ISingletonDependency
{
    private readonly ContractOptions _options;
    private readonly OracleInfoOptions _oracleOptions;
    private readonly IBlockchainClientFactory<AElfClient> _blockchainClientFactory;

    public ContractProvider(IBlockchainClientFactory<AElfClient> blockchainClientFactory,
        IOptionsSnapshot<ContractOptions> contractOptions, IOptionsSnapshot<OracleInfoOptions> oracleOptions)
    {
        _options = contractOptions.Value;
        _oracleOptions = oracleOptions.Value;
        _blockchainClientFactory = blockchainClientFactory;
    }

    public async Task<Hash> GetRandomHashAsync(long blockNumber, string chainId)
    {
        if (!_options.ChainInfos.TryGetValue(chainId, out var chainInfo)) return Hash.Empty;
        return await CallTransactionAsync<Hash>(chainId, await GenerateRawTransactionAsync(ContractConstants.GetRandomHash,
            new Int64Value { Value = blockNumber }, chainId, chainInfo.ConsensusContractAddress));
    }

    public async Task<GetConfigOutput> GetOracleConfigAsync(string chainId)
    {
        if (!_options.ChainInfos.TryGetValue(chainId, out var chainInfo)) return new GetConfigOutput();
        return await CallTransactionAsync<GetConfigOutput>(chainId,
            await GenerateRawTransactionAsync(ContractConstants.GetConfig, new Empty(), chainId,
                chainInfo.OracleContractAddress));
    }

    public async Task<Int64Value> GetLatestRoundAsync(string chainId)
    {
        if (!_options.ChainInfos.TryGetValue(chainId, out var chainInfo)) return new Int64Value();
        return await CallTransactionAsync<Int64Value>(chainId,
            await GenerateRawTransactionAsync(ContractConstants.GetLatestRound, new Empty(), chainId,
                chainInfo.OracleContractAddress));
    }

    public async Task<string> SendTransmitAsync(string chainId, TransmitInput transmitInput)
    {
        if (!_options.ChainInfos.TryGetValue(chainId, out var chainInfo)) return "";
        var txRes = await SendTransactionAsync(chainId, await GenerateRawTransactionAsync(ContractConstants.Transmit,
            transmitInput, chainId, chainInfo.OracleContractAddress));
        return txRes.TransactionId;
    }

    public async Task<long> GetBlockLatestHeightAsync(string chainId)
    {
        var client = _blockchainClientFactory.GetClient(chainId);
        return await client.GetBlockHeightAsync();
    }

    public async Task<Commitment> GetCommitmentAsync(string chainId, string transactionId)
    {
        var result = await GetTxResultAsync(chainId, transactionId);
        return Commitment.Parser.ParseFrom(ParseLogEvents<RequestStarted>(result).Commitment);
    }

    public Transmitted ParseTransmitted(TransactionResultDto transaction)
    {
        return ParseLogEvents<Transmitted>(transaction);
    }

    private async Task<T> CallTransactionAsync<T>(string chainId, string rawTx) where T : class, IMessage<T>, new()
    {
        var client = _blockchainClientFactory.GetClient(chainId);
        var result = await client.ExecuteTransactionAsync(new ExecuteTransactionDto() { RawTransaction = rawTx });
        var value = new T();
        value.MergeFrom(ByteArrayHelper.HexStringToByteArray(result));
        return value;
    }

    private async Task<SendTransactionOutput> SendTransactionAsync(string chainId, string rawTx)
    {
        var client = _blockchainClientFactory.GetClient(chainId);
        return await client.SendTransactionAsync(new SendTransactionInput { RawTransaction = rawTx });
    }

    private async Task<string> GenerateRawTransactionAsync(string methodName, IMessage param, string chainId,
        string contractAddress)
    {
        if (!_oracleOptions.ChainConfig.TryGetValue(chainId, out var chainInfo)) return "";
        var client = _blockchainClientFactory.GetClient(chainId);
        return client.SignTransaction(chainInfo.TransmitterSecret, await client.GenerateTransactionAsync(
                client.GetAddressFromPrivateKey(chainInfo.TransmitterSecret), contractAddress, methodName, param))
            .ToByteArray().ToHex();
    }

    public async Task<TransactionResultDto> GetTxResultAsync(string chainId, string transactionId)
    {
        var client = _blockchainClientFactory.GetClient(chainId);
        return await client.GetTransactionResultAsync(transactionId);
    }

    private static T ParseLogEvents<T>(TransactionResultDto txResult) where T : class, IMessage<T>, new()
    {
        var log = txResult.Logs.FirstOrDefault(l => l.Name == typeof(T).Name);
        if (log == null) return new T();

        var logEvent = new LogEvent
        {
            Indexed = { log.Indexed.Select(ByteString.FromBase64) },
            NonIndexed = ByteString.FromBase64(log.NonIndexed)
        };
        var transactionLogEvent = new T();
        transactionLogEvent.MergeFrom(logEvent.NonIndexed);
        foreach (var indexed in logEvent.Indexed)
        {
            transactionLogEvent.MergeFrom(indexed);
        }

        return transactionLogEvent;
    }
}