using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using AElf;
using AetherLink.Indexer.Dtos;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Nethereum.ABI.Decoders;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider.SearcherProvider;

public interface IEvmSearchWorkerProvider
{
    Task<long> GetStartHeightAsync(string networkName);
    Task SaveConsumedHeightAsync(string network, long height);
    Task<long> GetLatestBlockHeightAsync(Web3 web3);
    Task<List<EvmReceivedMessageDto>> GetEvmLogsAsync(Web3 web3, string contractAddress, long from, long to);
    Task StartCrossChainRequestsFromEvm(List<EvmReceivedMessageDto> requests);
}

public class EvmSearchWorkerProvider : IEvmSearchWorkerProvider, ISingletonDependency
{
    private readonly IStorageProvider _storageProvider;
    private readonly ILogger<EvmSearchWorkerProvider> _logger;
    private readonly ICrossChainRequestProvider _crossChainRequestProvider;


    public EvmSearchWorkerProvider(ILogger<EvmSearchWorkerProvider> logger, IStorageProvider storageProvider,
        ICrossChainRequestProvider crossChainRequestProvider)
    {
        _logger = logger;
        _storageProvider = storageProvider;
        _crossChainRequestProvider = crossChainRequestProvider;
    }

    public async Task<long> GetStartHeightAsync(string networkName)
    {
        var latestBlockHeight = await _storageProvider.GetAsync<SearchHeightDto>(GetSearchHeightRedisKey(networkName));
        return latestBlockHeight?.BlockHeight ?? 0;
    }

    public async Task SaveConsumedHeightAsync(string network, long height) =>
        await _storageProvider.SetAsync(GetSearchHeightRedisKey(network), new SearchHeightDto { BlockHeight = height });

    public async Task<long> GetLatestBlockHeightAsync(Web3 web3) =>
        (long)(await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value;

    public async Task<List<EvmReceivedMessageDto>> GetEvmLogsAsync(Web3 web3, string contractAddress,
        long fromBlockHeight, long toBlockHeight)
    {
        _logger.LogDebug(
            $"[EvmSearchWorkerProvider] Search {contractAddress} blocks from {fromBlockHeight} to {toBlockHeight}.");
        var filterInput = new NewFilterInput
        {
            FromBlock = new BlockParameter((ulong)fromBlockHeight),
            ToBlock = new BlockParameter((ulong)toBlockHeight),
            Address = new[] { contractAddress }
        };

        try
        {
            var logs = await web3.Eth.Filters.GetLogs.SendRequestAsync(filterInput);
            return logs.Select(DecodeFilterLog).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"[EvmSearchWorkerProvider] Search {contractAddress} blocks from {fromBlockHeight} to {toBlockHeight} failed.");
            throw;
        }
    }

    public async Task StartCrossChainRequestsFromEvm(List<EvmReceivedMessageDto> requests)
        => await Task.WhenAll(requests.Select(_crossChainRequestProvider.StartCrossChainRequestFromEvm));

    public async Task<List<FilterLog>> SearchRequestsAsync(EvmOptions options, long consumedBlockHeight)
    {
        var networkName = options.NetworkName;

        try
        {
            var web3 = new Web3(options.Api);
            var latestBlock = (long)(await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value;
            if (consumedBlockHeight + options.SubscribeBlocksDelay >= latestBlock)
            {
                _logger.LogInformation(
                    $"[EvmSearchWorkerProvider] {networkName} All blocks up to {latestBlock} have been processed.");
                return new();
            }

            _logger.LogInformation(
                $"[EvmSearchWorkerProvider] {networkName} Starting HTTP query from block {consumedBlockHeight} to latestBlock {latestBlock}");

            var from = consumedBlockHeight + 1;
            var pendingRequest = new List<FilterLog>();

            _logger.LogInformation(
                $"[EvmSearchWorkerProvider] {networkName} Processed blocks from {from} to {latestBlock}.");
            for (var curFrom = from; curFrom <= latestBlock; curFrom += EvmSubscribeConstants.SubscribeBlockStep)
            {
                var currentTo = Math.Min(curFrom + EvmSubscribeConstants.SubscribeBlockStep - 1, latestBlock);
                var filterInput = new NewFilterInput
                {
                    FromBlock = new BlockParameter((ulong)curFrom),
                    ToBlock = new BlockParameter((ulong)currentTo),
                    Address = new[] { options.ContractAddress }
                };

                try
                {
                    pendingRequest.AddRange(await web3.Eth.Filters.GetLogs.SendRequestAsync(filterInput));
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        $"[EvmSearchWorkerProvider] {networkName} Error processing blocks {curFrom} to {currentTo}: {ex.Message}");
                    throw;
                }
            }

            return pendingRequest;
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[EvmSearchWorkerProvider] {networkName} Error processing http subscribe.");
            return new();
        }
    }

    private EvmReceivedMessageDto DecodeFilterLog(FilterLog log)
    {
        EvmReceivedMessageDto messagePendingToCrossChain = null;
        try
        {
            var decoded = Event<SendEventDTO>.DecodeEvent(log);
            if (decoded != null) return GenerateEvmReceivedMessage(decoded);

            _logger.LogWarning($"[EvmSearchWorkerProvider] Failed to decode event at: {log.BlockNumber}");

            return new();
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[EvmSearchWorkerProvider] Decode {log.TransactionHash} fail at {log.BlockNumber}.");
            throw;
        }
    }

    public async Task DecodeAndStartCrossChainAsync(FilterLog log)
    {
        EvmReceivedMessageDto messagePendingToCrossChain = null;
        try
        {
            var decoded = Event<SendEventDTO>.DecodeEvent(log);
            if (decoded == null)
            {
                _logger.LogWarning($"[EvmSearchWorkerProvider] Failed to decode event at: {log.BlockNumber}");
                return;
            }

            messagePendingToCrossChain = GenerateEvmReceivedMessage(decoded);
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[EvmSearchWorkerProvider] Decode {log.TransactionHash} fail at {log.BlockNumber}.");
        }

        await _crossChainRequestProvider.StartCrossChainRequestFromEvm(messagePendingToCrossChain);

        _logger.LogDebug(
            $"[EvmSearchWorkerProvider] Start cross chain request {messagePendingToCrossChain.MessageId} successful at: {log.TransactionHash} {log.BlockNumber}");
    }

    private EvmReceivedMessageDto GenerateEvmReceivedMessage(EventLog<SendEventDTO> eventData)
    {
        var blockNumber = eventData.Log.BlockNumber;
        var sendRequestData = eventData.Event;
        var messageId = ByteString.CopyFrom(sendRequestData.MessageId).ToBase64();
        var sender = ByteStringHelper.FromHexString(sendRequestData.Sender).ToBase64();
        var receivedMessage = new EvmReceivedMessageDto
        {
            MessageId = messageId,
            Epoch = (long)sendRequestData.Epoch,
            SourceChainId = (long)sendRequestData.SourceChainId,
            TargetChainId = (long)sendRequestData.TargetChainId,
            Sender = sender,
            Receiver = sendRequestData.Receiver,
            Message = Convert.ToBase64String(sendRequestData.Message),
            BlockNumber = blockNumber,
            TransactionTime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds()
        };

        if (sendRequestData.TokenTransferMetadataBytes.Length > 0)
        {
            receivedMessage.TokenTransferMetadataInfo =
                DecodeTokenTransferMetadata(sendRequestData.TokenTransferMetadataBytes);
        }

        _logger.LogInformation(
            $"[EvmSearchWorkerProvider] Get evm cross chain request {eventData.Log.TransactionHash} {messageId} from {sender} to {(long)sendRequestData.TargetChainId} {sendRequestData.Receiver}");
        return receivedMessage;
    }

    private TokenTransferMetadataDto DecodeTokenTransferMetadata(byte[] metadataBytes)
    {
        try
        {
            var uintDecoder = new IntTypeDecoder();
            var offset = 32;

            BigInteger targetChainId = uintDecoder.DecodeBigInteger(metadataBytes.Skip(offset).Take(32).ToArray());
            offset += 32;

            var tokenAddressOffset =
                (int)uintDecoder.DecodeBigInteger(metadataBytes.Skip(offset).Take(32).ToArray());
            offset += 32;

            // var symbolOffset = (int)uintDecoder.DecodeBigInteger(metadataBytes.Skip(offset).Take(32).ToArray());
            // offset += 32;

            BigInteger amountRaw = uintDecoder.DecodeBigInteger(metadataBytes.Skip(offset).Take(32).ToArray());
            BigInteger amount = amountRaw / BigInteger.Pow(10, 18);

            // offset += 32;
            // var extraDataOffset = (int)uintDecoder.DecodeBigInteger(metadataBytes.Skip(offset).Take(32).ToArray());

            var tokenAddressLength =
                (int)uintDecoder.DecodeBigInteger(metadataBytes.Skip(tokenAddressOffset + 32).Take(32).ToArray());

            var tokenAddress =
                Encoding.UTF8.GetString(metadataBytes.Skip(tokenAddressOffset + 64).Take(tokenAddressLength)
                    .ToArray());
            var checksumAddress = new AddressUtil().ConvertToChecksumAddress(tokenAddress);

            // var symbolLength =
            //     (int)uintDecoder.DecodeBigInteger(metadataBytes.Skip(symbolOffset + 32).Take(32).ToArray());
            // var symbol = Encoding.UTF8.GetString(metadataBytes.Skip(symbolOffset + 64).Take(symbolLength).ToArray());
            // var extraDataLength =
            //     (int)uintDecoder.DecodeBigInteger(metadataBytes.Skip(extraDataOffset + 32).Take(32).ToArray());
            // var extraData = metadataBytes.Skip(extraDataOffset + 64).Take(extraDataLength).ToArray();

            _logger.LogDebug(
                $"[EvmSearchWorkerProvider] Get cross chain token transfer metadata => targetChainId:{targetChainId}, tokenAddress: {checksumAddress}, amount: {amount}");
            return new()
            {
                TargetChainId = (long)targetChainId,
                TokenAddress = checksumAddress,
                Amount = (long)amount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"[EvmSearchWorkerProvider] Error decoding TokenTransferMetadataBytes: {ex.Message}");
            return new();
        }
    }

    private static string GetSearchHeightRedisKey(string chainId)
        => IdGeneratorHelper.GenerateId(RedisKeyConstants.SearchHeightKey, chainId);
}