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
    private readonly ConcurrentDictionary<string, NetworkState> _networkStates = new();

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
            await DealingWithMissedDataAsync(indexerOptions);
            await StartSubscribeAndRunAsync(indexerOptions);

            await Task.Delay(RetryConstants.DefaultDelay * 1000);
            _logger.LogDebug($"[EvmEventSubscriber] Starting {indexerOptions.NetworkName} subscribe in next round.");
        }
    }

    private async Task StartSubscribeAndRunAsync(EvmOptions networkOptions)
    {
        var networkName = networkOptions.NetworkName;
        if (!_networkStates.TryGetValue(networkName, out var networkState))
        {
            _logger.LogError($"[EvmEventSubscriber] {networkName} Network state is empty, please check options.");
            return;
        }

        if (!networkState.IsHttpFinished)
        {
            _logger.LogWarning($"[EvmEventSubscriber] {networkName} http handler is no finished.");
            return;
        }

        _logger.LogInformation($"[EvmEventSubscriber] Starting ws subscription on Network: {networkName}");

        using var client = new StreamingWebSocketClient(networkOptions.WsUrl);
        try
        {
            networkState.IsWsRunning = true;

            await SubscribeToEventsAsync(client, networkOptions);

            while (networkState.IsWsRunning)
            {
                var handler = new EthBlockNumberObservableHandler(client);

                handler.GetResponseAsObservable().Subscribe(async x =>
                {
                    await SaveConsumedBlockHeightAsync(networkName, (long)x.Value);
                    networkState.LastProcessedBlock = (long)x.Value;

                    _logger.LogDebug($"[EvmEventSubscriber] {networkName} BlockHeight: {x.Value}");
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
            networkState.IsWsRunning = false;
            if (client != null) await client.StopAsync();

            _logger.LogDebug($"[EvmEventSubscriber] Network: {networkName} WebSocket client stopped.");
        }
    }

    private async Task DealingWithMissedDataAsync(EvmOptions options)
    {
        var networkName = options.NetworkName;
        if (!_networkStates.TryGetValue(networkName, out var networkState))
        {
            _logger.LogError($"[EvmEventSubscriber] {networkName} Network state is empty, please check options.");
            return;
        }

        if (networkState.LastProcessedBlock == 0)
        {
            _logger.LogInformation($"[EvmEventSubscriber] {networkName} No altitude compensation required.");
            return;
        }

        if (networkState.IsWsRunning)
        {
            _logger.LogInformation(
                $"[EvmEventSubscriber] {networkName} WebSocket is running. HTTP will wait for WS to stop.");
            return;
        }

        try
        {
            var lastProcessedBlock = networkState.LastProcessedBlock;
            var web3 = new Web3(options.Api);

            while (true)
            {
                networkState.IsHttpFinished = false;
                var latestBlock = (long)(await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value;
                if (lastProcessedBlock >= latestBlock)
                {
                    _logger.LogInformation(
                        $"[EvmEventSubscriber] {networkName} All blocks up to {latestBlock} have been processed.");
                    networkState.IsHttpFinished = true;
                    break;
                }

                _logger.LogInformation(
                    $"[EvmEventSubscriber] {networkName} Starting HTTP query from block {lastProcessedBlock} to latestBlock {latestBlock}");

                var from = lastProcessedBlock + 1;

                for (var currentFrom = from;
                     currentFrom <= latestBlock;
                     currentFrom += EvmSubscribeConstants.SubscribeBlockStep)
                {
                    var currentTo = Math.Min(currentFrom + EvmSubscribeConstants.SubscribeBlockStep - 1, latestBlock);
                    var filterInput = new NewFilterInput
                    {
                        FromBlock = new BlockParameter((ulong)currentFrom),
                        ToBlock = new BlockParameter((ulong)currentTo),
                        Address = new[] { options.ContractAddress }
                    };

                    try
                    {
                        var logs = await web3.Eth.Filters.GetLogs.SendRequestAsync(filterInput);
                        var tasks = logs.Select(DecodeAndStartCrossChainAsync);
                        await Task.WhenAll(tasks);

                        lastProcessedBlock = currentTo;
                        networkState.LastProcessedBlock = currentTo;

                        await SaveConsumedBlockHeightAsync(networkName, currentTo);

                        _logger.LogInformation(
                            $"[EvmEventSubscriber] {networkName} Processed blocks from {currentFrom} to {currentTo}.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            $"[EvmEventSubscriber] {networkName} Error processing blocks {currentFrom} to {currentTo}: {ex.Message}");
                        throw;
                    }
                }

                networkState.IsHttpFinished = true;
                _logger.LogInformation(
                    $"[EvmEventSubscriber] {networkName} http processing blocks is finished.");
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[EvmEventSubscriber] {networkName} Error processing http subscribe.");
        }
    }

    private async Task SubscribeToEventsAsync(StreamingWebSocketClient client, EvmOptions options)
    {
        var networkName = options.NetworkName;
        if (!_networkStates.TryGetValue(networkName, out var networkState))
        {
            _logger.LogError($"[EvmEventSubscriber] {networkName} Network state is empty, please check options.");
            return;
        }

        var eventSubscription = new EthLogsObservableSubscription(client);
        var eventFilterInput = Event<SendEventDTO>.GetEventABI().CreateFilterInput(options.ContractAddress);

        eventSubscription.GetSubscriptionDataResponsesAsObservable().Subscribe(
            async log =>
            {
                await DecodeAndStartCrossChainAsync(log);

                var blockHeight = (long)log.BlockNumber.Value;
                networkState.LastProcessedBlock = blockHeight;
                await SaveConsumedBlockHeightAsync(networkName, blockHeight);
            },
            exception => _logger.LogError($"[EvmEventSubscriber] {networkName} Subscription error: {exception.Message}")
        );

        _logger.LogDebug($"[EvmEventSubscriber] Connecting WebSocket client on {networkName}...");

        await client.StartAsync();
        await eventSubscription.SubscribeAsync(eventFilterInput);

        _logger.LogDebug($"[EvmEventSubscriber] Successfully subscribed to contract events on {networkName}.");
    }

    private async Task DecodeAndStartCrossChainAsync(FilterLog log)
    {
        try
        {
            var decoded = Event<SendEventDTO>.DecodeEvent(log);
            if (decoded == null)
            {
                _logger.LogWarning($"[EvmEventSubscriber] Failed to decode event at: {log.BlockNumber}");
                return;
            }

            var messagePendingToCrossChain = GenerateEvmReceivedMessage(decoded);
            await _crossChainProvider.StartCrossChainRequestFromEvm(messagePendingToCrossChain);

            _logger.LogDebug(
                $"[EvmEventSubscriber] Start cross chain request {messagePendingToCrossChain.MessageId} successful at: {log.TransactionHash} {log.BlockNumber}");
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[EvmEventSubscriber] Decode {log.TransactionHash} fail at {log.BlockNumber}.");
        }
    }

    private async Task InitializeConsumedHeight(EvmContractsOptions options)
    {
        _logger.LogDebug("[EvmEventSubscriber] Start consumption height setting");

        foreach (var op in options.ContractConfig.Values)
        {
            var redisKey = IdGeneratorHelper.GenerateId(RedisKeyConstants.SearchHeightKey, op.NetworkName);
            var latestBlockHeight = await _storageProvider.GetAsync<SearchHeightDto>(redisKey);

            _logger.LogDebug(
                $"[EvmEventSubscriber] {op.NetworkName} has consumed {latestBlockHeight} with {redisKey}.");

            _networkStates[op.NetworkName] = new() { LastProcessedBlock = latestBlockHeight?.BlockHeight ?? 0 };
        }
    }

    private async Task SaveConsumedBlockHeightAsync(string network, long blockHeight)
    {
        var redisKey = IdGeneratorHelper.GenerateId(RedisKeyConstants.SearchHeightKey, network);
        await _storageProvider.SetAsync(redisKey, new SearchHeightDto { BlockHeight = blockHeight });

        _logger.LogDebug(
            $"[EvmEventSubscriber] Network {network} has consumed to height {blockHeight} with {redisKey}.");
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