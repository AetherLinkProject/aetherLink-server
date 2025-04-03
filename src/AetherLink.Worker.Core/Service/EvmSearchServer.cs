using System;
using System.Collections.Concurrent;
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
using AetherLink.Worker.Core.Provider;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.ABI.Decoders;
using Volo.Abp.DependencyInjection;
using Nethereum.Contracts;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Reactive.Eth;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.Util;
using Nethereum.Web3;

namespace AetherLink.Worker.Core.Service;

public interface IEvmSearchServer
{
    Task StartAsync();
}

public class EvmSearchServer : IEvmSearchServer, ISingletonDependency
{
    private readonly EvmContractsOptions _evmOptions;
    private readonly ILogger<EvmSearchServer> _logger;
    private readonly IStorageProvider _storageProvider;
    private readonly ICrossChainRequestProvider _crossChainProvider;
    private readonly ConcurrentDictionary<string, bool> _healthMap = new();
    private readonly ConcurrentDictionary<string, long> _consumedWsBlockHeights = new();
    private readonly ConcurrentDictionary<string, long> _consumedHttpBlockHeights = new();

    public EvmSearchServer(ILogger<EvmSearchServer> logger, ICrossChainRequestProvider crossChainProvider,
        IStorageProvider storageProvider, IOptionsSnapshot<EvmContractsOptions> evmOptions)
    {
        _logger = logger;
        _evmOptions = evmOptions.Value;
        _storageProvider = storageProvider;
        _crossChainProvider = crossChainProvider;
    }

    public async Task StartAsync()
    {
        _logger.LogDebug("[EvmSearchServer] Starting EvmSearchServer ....");
        await InitializeConsumedHeight(_evmOptions);

        await Task.WhenAll(_evmOptions.ContractConfig.Values.Select(StartSubscribeEvmEventsAsync));
    }

    private async Task StartSubscribeEvmEventsAsync(EvmOptions indexerOptions)
    {
        while (true)
        {
            await Task.WhenAll(StartSubscribeAndRunAsync(indexerOptions), DealingWithMissedDataAsync(indexerOptions));

            _logger.LogDebug($"[EvmEventSubscriber] Starting {indexerOptions.NetworkName} subscribe in next round.");

            await Task.Delay(RetryConstants.DefaultDelay * 1000);
            _healthMap[indexerOptions.NetworkName] = true;
        }
    }

    private async Task StartSubscribeAndRunAsync(EvmOptions networkOptions)
    {
        var networkName = networkOptions.NetworkName;
        if (!_consumedWsBlockHeights.TryGetValue(networkName, out _))
        {
            _logger.LogError($"[EvmEventSubscriber] {networkName} Network state is empty, please check options.");
            return;
        }

        _logger.LogInformation($"[EvmEventSubscriber] Starting ws subscription on Network: {networkName}");

        using var client = new StreamingWebSocketClient(networkOptions.WsUrl);
        try
        {
            await SubscribeToEventsAsync(client, networkOptions);

            while (_healthMap[networkName])
            {
                var handler = new EthBlockNumberObservableHandler(client);

                handler.GetResponseAsObservable().Subscribe(async x =>
                {
                    var blockHeight = (long)x.Value;
                    _logger.LogDebug($"[EvmEventSubscriber] {networkName} BlockHeight: {blockHeight}");

                    if (blockHeight % EvmSubscribeConstants.SubscribeBlockSaveStep != 0) return;

                    _consumedWsBlockHeights[networkName] = blockHeight;
                    await SaveWebsocketSubscribedHeightAsync(networkName, blockHeight);
                });

                await handler.SendRequestAsync();
                await Task.Delay(networkOptions.PingDelay);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[EvmEventSubscriber] {networkName} subscription failed.");
        }
        finally
        {
            _healthMap[networkName] = false;
            if (client != null) await client.StopAsync();

            _logger.LogDebug($"[EvmEventSubscriber] Network: {networkName} WebSocket client stopped.");
        }
    }

    private async Task DealingWithMissedDataAsync(EvmOptions options)
    {
        var networkName = options.NetworkName;
        if (!_consumedHttpBlockHeights.TryGetValue(networkName, out var consumedHttpBlockHeight) ||
            !_consumedWsBlockHeights.TryGetValue(networkName, out var consumedWsBlockHeight))
        {
            _logger.LogError($"[EvmEventSubscriber] {networkName} Network state is empty, please check options.");
            return;
        }

        if (!_healthMap[networkName])
        {
            _logger.LogInformation($"[EvmEventSubscriber] {networkName} network is not health.");
            return;
        }

        try
        {
            var lastProcessedBlock = Math.Min(consumedHttpBlockHeight, consumedWsBlockHeight);
            var web3 = new Web3(options.Api);
            var latestBlock = (long)(await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value;
            if (consumedHttpBlockHeight == 0)
            {
                _logger.LogInformation($"[EvmEventSubscriber] {networkName} No altitude compensation required.");

                await SaveHttpConsumedHeightAsync(networkName, latestBlock);
                _consumedHttpBlockHeights[networkName] = latestBlock;

                return;
            }

            if (lastProcessedBlock >= latestBlock)
            {
                _logger.LogInformation(
                    $"[EvmEventSubscriber] {networkName} All blocks up to {latestBlock} have been processed.");
                return;
            }

            _logger.LogInformation(
                $"[EvmEventSubscriber] {networkName} Starting HTTP query from block {lastProcessedBlock} to latestBlock {latestBlock}");

            var from = lastProcessedBlock + 1;
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
                    var logs = await web3.Eth.Filters.GetLogs.SendRequestAsync(filterInput);
                    var tasks = logs.Select(DecodeAndStartCrossChainAsync);
                    await Task.WhenAll(tasks);

                    _consumedHttpBlockHeights[networkName] = currentTo;

                    await SaveHttpConsumedHeightAsync(networkName, currentTo);

                    _logger.LogInformation(
                        $"[EvmEventSubscriber] {networkName} Processed blocks from {curFrom} to {currentTo}.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        $"[EvmEventSubscriber] {networkName} Error processing blocks {curFrom} to {currentTo}: {ex.Message}");
                    throw;
                }
            }
        }
        catch (Exception e)
        {
            _healthMap[networkName] = false;
            _logger.LogError(e, $"[EvmEventSubscriber] {networkName} Error processing http subscribe.");
        }
    }

    private async Task SubscribeToEventsAsync(StreamingWebSocketClient client, EvmOptions options)
    {
        var networkName = options.NetworkName;
        if (!_consumedWsBlockHeights.TryGetValue(networkName, out var consumedWsBlockHeight))
        {
            _logger.LogError($"[EvmEventSubscriber] {networkName} Network state is empty, please check options.");
            return;
        }

        var eventSubscription = new EthLogsObservableSubscription(client);
        var eventFilterInput = Event<SendEventDTO>.GetEventABI().CreateFilterInput(options.ContractAddress);

        eventSubscription.GetSubscriptionDataResponsesAsObservable().Subscribe(async log =>
            {
                await DecodeAndStartCrossChainAsync(log);

                var blockHeight = (long)log.BlockNumber.Value;
                consumedWsBlockHeight = blockHeight;
                await SaveWebsocketSubscribedHeightAsync(networkName, blockHeight);
            },
            exception =>
            {
                _logger.LogError($"[EvmEventSubscriber] {networkName} Subscription error: {exception.Message}");

                _healthMap[networkName] = false;
                client?.StopAsync();

                throw exception;
            });

        _logger.LogDebug($"[EvmEventSubscriber] Connecting WebSocket client on {networkName}...");

        await client.StartAsync();
        await eventSubscription.SubscribeAsync(eventFilterInput);

        _logger.LogDebug($"[EvmEventSubscriber] Successfully subscribed to contract events on {networkName}.");
    }

    private async Task DecodeAndStartCrossChainAsync(FilterLog log)
    {
        EvmReceivedMessageDto messagePendingToCrossChain = null;
        try
        {
            var decoded = Event<SendEventDTO>.DecodeEvent(log);
            if (decoded == null)
            {
                _logger.LogWarning($"[EvmEventSubscriber] Failed to decode event at: {log.BlockNumber}");
                return;
            }

            messagePendingToCrossChain = GenerateEvmReceivedMessage(decoded);
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[EvmEventSubscriber] Decode {log.TransactionHash} fail at {log.BlockNumber}.");
        }

        await _crossChainProvider.StartCrossChainRequestFromEvm(messagePendingToCrossChain);

        _logger.LogDebug(
            $"[EvmEventSubscriber] Start cross chain request {messagePendingToCrossChain.MessageId} successful at: {log.TransactionHash} {log.BlockNumber}");
    }

    private async Task InitializeConsumedHeight(EvmContractsOptions options)
    {
        _logger.LogDebug("[EvmEventSubscriber] Start consumption height setting");

        foreach (var op in options.ContractConfig.Values)
        {
            var redisKey =
                IdGeneratorHelper.GenerateId(RedisKeyConstants.EvmWebsocketSubscribedHeightKey, op.NetworkName);
            var latestWebsocketSubscribedBlockHeight = await _storageProvider.GetAsync<SearchHeightDto>(redisKey);
            _consumedWsBlockHeights[op.NetworkName] = latestWebsocketSubscribedBlockHeight?.BlockHeight ?? 0;

            var httpRedisKey =
                IdGeneratorHelper.GenerateId(RedisKeyConstants.EvmHttpConsumedHeightKey, op.NetworkName);
            var latestHttpConsumedBlockHeight = await _storageProvider.GetAsync<SearchHeightDto>(httpRedisKey);
            _consumedHttpBlockHeights[op.NetworkName] = latestHttpConsumedBlockHeight?.BlockHeight ?? 0;

            _logger.LogDebug(
                $"[EvmEventSubscriber] {op.NetworkName} websocket has subscribed {_consumedWsBlockHeights[op.NetworkName]}, http has consumed {_consumedHttpBlockHeights[op.NetworkName]}.");

            _healthMap[op.NetworkName] = true;
        }
    }

    private async Task SaveWebsocketSubscribedHeightAsync(string network, long blockHeight)
        => await SaveBlockHeightAsync(
            IdGeneratorHelper.GenerateId(RedisKeyConstants.EvmWebsocketSubscribedHeightKey, network), blockHeight);

    private async Task SaveHttpConsumedHeightAsync(string network, long blockHeight)
        => await SaveBlockHeightAsync(
            IdGeneratorHelper.GenerateId(RedisKeyConstants.EvmHttpConsumedHeightKey, network), blockHeight);

    private async Task SaveBlockHeightAsync(string redisKey, long blockHeight)
    {
        await _storageProvider.SetAsync(redisKey, new SearchHeightDto { BlockHeight = blockHeight });

        _logger.LogDebug($"[EvmEventSubscriber] {redisKey} has consumed to height {blockHeight}.");
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
            $"[EvmSearchServer] Get evm cross chain request {eventData.Log.TransactionHash} {messageId} from {sender} to {(long)sendRequestData.TargetChainId} {sendRequestData.Receiver}");
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

            var tokenAddressOffset = (int)uintDecoder.DecodeBigInteger(metadataBytes.Skip(offset).Take(32).ToArray());
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
                Encoding.UTF8.GetString(metadataBytes.Skip(tokenAddressOffset + 64).Take(tokenAddressLength).ToArray());
            var checksumAddress = new AddressUtil().ConvertToChecksumAddress(tokenAddress);

            // var symbolLength =
            //     (int)uintDecoder.DecodeBigInteger(metadataBytes.Skip(symbolOffset + 32).Take(32).ToArray());
            // var symbol = Encoding.UTF8.GetString(metadataBytes.Skip(symbolOffset + 64).Take(symbolLength).ToArray());
            // var extraDataLength =
            //     (int)uintDecoder.DecodeBigInteger(metadataBytes.Skip(extraDataOffset + 32).Take(32).ToArray());
            // var extraData = metadataBytes.Skip(extraDataOffset + 64).Take(extraDataLength).ToArray();

            _logger.LogDebug(
                $"[EvmSearchServer] Get cross chain token transfer metadata => targetChainId:{targetChainId}, tokenAddress: {checksumAddress}, amount: {amount}");
            return new()
            {
                TargetChainId = (long)targetChainId,
                TokenAddress = checksumAddress,
                Amount = (long)amount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"[EvmSearchServer] Error decoding TokenTransferMetadataBytes: {ex.Message}");
            return new();
        }
    }
}