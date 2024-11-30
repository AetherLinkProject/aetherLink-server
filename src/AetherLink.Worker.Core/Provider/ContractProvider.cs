using System;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Client.Dto;
using AElf.Client.Service;
using AElf.CSharp.Core;
using AElf.Types;
using AetherLink.Contracts.Oracle;
using AetherLink.Contracts.Ramp;
using AetherLink.Worker.Core.Common.ContractHandler;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Options;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
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
    public Task<string> SendCommitAsync(string chainId, CommitInput commitInput);
    public Task<long> GetBlockLatestHeightAsync(string chainId);
    public Task<Commitment> GetCommitmentAsync(string chainId, string transactionId);
    public Task<TransactionResultDto> GetTxResultAsync(string chainId, string transactionId);
    public Transmitted ParseTransmitted(TransactionResultDto transaction);
    public Task<bool> IsTransactionConfirmed(string chainId, long blockHeight, string blockHash);

    public Task<string> SendTransmitWithRefHashAsync(string chainId, TransmitInput transmitInput,
        long refBlockNumber, string refBlockHash);
}

public class ContractProvider : IContractProvider, ISingletonDependency
{
    private readonly ContractOptions _options;
    private readonly OracleInfoOptions _oracleOptions;
    private readonly ILogger<ContractProvider> _logger;
    private readonly IBlockchainClientFactory<AElfClient> _blockchainClientFactory;

    public ContractProvider(IBlockchainClientFactory<AElfClient> blockchainClientFactory,
        IOptionsSnapshot<ContractOptions> contractOptions, IOptionsSnapshot<OracleInfoOptions> oracleOptions,
        ILogger<ContractProvider> logger)
    {
        _logger = logger;
        _options = contractOptions.Value;
        _oracleOptions = oracleOptions.Value;
        _blockchainClientFactory = blockchainClientFactory;
    }

    public async Task<Hash> GetRandomHashAsync(long blockNumber, string chainId)
    {
        if (!_options.ChainInfos.TryGetValue(chainId, out var chainInfo)) return Hash.Empty;
        return await CallTransactionAsync<Hash>(chainId, await GenerateRawTransactionAsync(
            ContractConstants.GetRandomHash, new Int64Value { Value = blockNumber }, chainId,
            chainInfo.ConsensusContractAddress));
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

    public async Task<long> GetBlockLatestHeightAsync(string chainId)
        => await _blockchainClientFactory.GetClient(chainId).GetBlockHeightAsync();

    public async Task<Commitment> GetCommitmentAsync(string chainId, string transactionId)
        => Commitment.Parser.ParseFrom(ParseLogEvents<RequestStarted>(await GetTxResultAsync(chainId, transactionId))
            .Commitment);

    public async Task<TransactionResultDto> GetTxResultAsync(string chainId, string transactionId)
    {
        var result = await _blockchainClientFactory.GetClient(chainId).GetTransactionResultAsync(transactionId);
        _logger.LogDebug($"[ContractProvider]{result.TransactionId} status: {result.Status} err: {result.Error}");
        return result;
    }

    public async Task<string> SendTransmitAsync(string chainId, TransmitInput transmitInput)
    {
        if (!_options.ChainInfos.TryGetValue(chainId, out var chainInfo)) return "";
        var txRes = await SendTransactionAsync(chainId, await GenerateRawTransactionAsync(ContractConstants.Transmit,
            transmitInput, chainId, chainInfo.OracleContractAddress));
        return txRes.TransactionId;
    }

    public async Task<string> SendCommitAsync(string chainId, CommitInput commitInput)
    {
        if (!_options.ChainInfos.TryGetValue(chainId, out var chainInfo)) return "";
        var rawTransaction = await GenerateRawTransactionAsync(ContractConstants.Commit,
            commitInput, chainId, chainInfo.RampContractAddress);
        var txRes = await SendTransactionAsync(chainId, rawTransaction);
        var transactionId = txRes.TransactionId;
        _logger.LogDebug($"[ContractProvider] {transactionId} rawTransaction: {rawTransaction}");
        return transactionId;
    }

    public async Task<bool> IsTransactionConfirmed(string chainId, long blockHeight, string blockHash)
    {
        return _options.ChainInfos.TryGetValue(chainId, out _) && blockHash ==
            (await _blockchainClientFactory.GetClient(chainId).GetBlockByHeightAsync(blockHeight))?.BlockHash;
    }

    public async Task<string> SendTransmitWithRefHashAsync(string chainId, TransmitInput transmitInput,
        long refBlockNumber, string refBlockHash)
    {
        var client = _blockchainClientFactory.GetClient(chainId);

        if ((await client.GetBlockHeightAsync()).Sub(refBlockNumber) > 512)
            return await SendTransmitAsync(chainId, transmitInput);

        if (!_oracleOptions.ChainConfig.TryGetValue(chainId, out var oracleConfig) ||
            !_options.ChainInfos.TryGetValue(chainId, out var chainInfo))
            throw new Exception($"Send transmit with refHash Chain {chainId} not supported");

        var rawTx = client.SignTransaction(oracleConfig.TransmitterSecret, new Transaction
        {
            From = Address.FromBase58(client.GetAddressFromPrivateKey(oracleConfig.TransmitterSecret)),
            To = Address.FromBase58(chainInfo.OracleContractAddress),
            MethodName = ContractConstants.Transmit,
            Params = transmitInput.ToByteString(),
            RefBlockNumber = refBlockNumber,
            RefBlockPrefix = ByteString.CopyFrom(Hash.LoadFromHex(refBlockHash).Value.Take(4).ToArray())
        }).ToByteArray().ToHex();

        var txRes = await SendTransactionAsync(chainId, rawTx);
        return txRes.TransactionId;
    }

    public Transmitted ParseTransmitted(TransactionResultDto transaction) => ParseLogEvents<Transmitted>(transaction);

    private async Task<T> CallTransactionAsync<T>(string chainId, string rawTx) where T : class, IMessage<T>, new()
    {
        var client = _blockchainClientFactory.GetClient(chainId);
        var result = await client.ExecuteTransactionAsync(new() { RawTransaction = rawTx });
        var value = new T();
        value.MergeFrom(ByteArrayHelper.HexStringToByteArray(result));
        return value;
    }

    private async Task<SendTransactionOutput> SendTransactionAsync(string chainId, string rawTx)
        => await _blockchainClientFactory.GetClient(chainId)
            .SendTransactionAsync(new SendTransactionInput { RawTransaction = rawTx });

    private async Task<string> GenerateRawTransactionAsync(string methodName, IMessage param, string chainId,
        string contractAddress)
    {
        if (!_oracleOptions.ChainConfig.TryGetValue(chainId, out var chainInfo)) return "";
        var client = _blockchainClientFactory.GetClient(chainId);
        return client.SignTransaction(chainInfo.TransmitterSecret, await client.GenerateTransactionAsync(
                client.GetAddressFromPrivateKey(chainInfo.TransmitterSecret), contractAddress, methodName, param))
            .ToByteArray().ToHex();
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