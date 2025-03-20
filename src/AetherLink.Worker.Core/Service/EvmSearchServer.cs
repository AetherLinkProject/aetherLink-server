using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using AElf;
using AetherLink.Indexer;
using AetherLink.Indexer.Dtos;
using AetherLink.Indexer.Provider;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.ABI.Decoders;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Volo.Abp.DependencyInjection;
using Nethereum.Contracts;
using Nethereum.JsonRpc.WebSocketStreamingClient;
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
    private readonly ILogger<EvmSearchServer> _logger;
    private readonly IEvmRpcProvider _indexerProvider;
    private readonly IStorageProvider _storageProvider;
    private readonly EvmIndexerOptionsMap _networkOptions;
    private readonly ICrossChainRequestProvider _crossChainProvider;
    private readonly ConcurrentDictionary<string, long> _heightMap = new();

    public EvmSearchServer(ILogger<EvmSearchServer> logger, ICrossChainRequestProvider crossChainProvider,
        IEvmRpcProvider indexerProvider, IOptionsSnapshot<EvmIndexerOptionsMap> networkOptions,
        IStorageProvider storageProvider)
    {
        _logger = logger;
        _indexerProvider = indexerProvider;
        _storageProvider = storageProvider;
        _networkOptions = networkOptions.Value;
        _crossChainProvider = crossChainProvider;
    }

    public async Task StartAsync()
    {
        _logger.LogDebug("[EvmSearchServer] Starting EvmSearchServer ....");
        await InitializeConsumedHeight(_networkOptions);

        await Task.WhenAll(_networkOptions.ChainInfos.Values.Select(SubscribeRequestAsync));
    }

    private async Task SubscribeRequestAsync(EvmIndexerOptions options)
    {
        try
        {
            await _indexerProvider.SubscribeAndRunAsync<SendEventDTO>(
                options, eventData =>
                {
                    _logger.LogInformation("[EvmSearchServer] Received Event --> ");
                    _crossChainProvider.StartCrossChainRequestFromEvm(GenerateEvmReceivedMessage(eventData));
                });

            _logger.LogInformation("[EvmSearchServer] Start handler cross chain request... ");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[EvmSearchServer] Subscribe cross chain request fail.");
            throw;
        }
    }

    private async Task DealingWithMissedDataAsync<TEventDTO>(EvmOptions networkOptions,
        Action<EventLog<TEventDTO>> onEventDecoded) where TEventDTO : IEventDTO, new()
    {
        var lastProcessedBlock = _heightMap[networkOptions.NetworkName];
        _logger.LogInformation($"[EvmSearchServer] Last processed block: {lastProcessedBlock}");

        var web3 = new Web3(networkOptions.Api);
        var latestBlock = (long)(await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value;
        _logger.LogInformation($"[EvmSearchServer] Current latest block: {latestBlock}");

        await QueryHistoricalEventsAsync<TEventDTO>(web3, networkOptions,
            lastProcessedBlock + 1, latestBlock - 1, onEventDecoded);
    }

    private async Task QueryHistoricalEventsAsync<TEventDTO>(Web3 web3, EvmOptions networkOptions, long from, long to,
        Action<EventLog<TEventDTO>> onEventDecoded) where TEventDTO : IEventDTO, new()
    {
    }

    public async Task SubscribeAndRunAsync<TEventDTO>(EvmOptions networkOptions,
        Action<EventLog<TEventDTO>> onEventDecoded) where TEventDTO : IEventDTO, new()
    {
        _logger.LogInformation($"[EvmRpcProvider] Starting subscription on Network: {networkOptions.NetworkName}");

        // initial retry time == 1 min
        var retryInterval = 1000;

        while (true)
        {
            using var client = new StreamingWebSocketClient(networkOptions.WsUrl);

            try
            {
                await SubscribeToEventsAsync(client, networkOptions, onEventDecoded);

                while (true)
                {
                    var handler = new EthBlockNumberObservableHandler(client);
                    handler.GetResponseAsObservable().Subscribe(x =>
                        _logger.LogDebug(
                            $"[EvmRpcProvider] Network: {networkOptions.NetworkName} BlockHeight: {x.Value}"));
                    await handler.SendRequestAsync();
                    await Task.Delay(networkOptions.PingDelay);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    $"[EvmRpcProvider] Network: {networkOptions.NetworkName} subscription failed. Retrying...");

                // back off retry in 1 min
                if (retryInterval < 60000) retryInterval *= 2;

                await Task.Delay(retryInterval);
            }
            finally
            {
                if (client != null) await client.StopAsync();
                _logger.LogDebug(
                    $"[EvmRpcProvider] Network: {networkOptions.NetworkName} WebSocket client stopped. Restarting...");
            }
        }
    }

    private async Task InitializeConsumedHeight(EvmIndexerOptionsMap options)
    {
        foreach (var op in options.ChainInfos)
        {
            var redisKey = IdGeneratorHelper.GenerateId(RedisKeyConstants.SearchHeightKey, op.Key);
            var latestBlockHeight = await _storageProvider.GetAsync<SearchHeightDto>(redisKey);

            _heightMap[op.Key] = latestBlockHeight?.BlockHeight ?? 0;
        }
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


    private async Task SubscribeToEventsAsync<TEventDTO>(StreamingWebSocketClient client, EvmOptions options,
        Action<EventLog<TEventDTO>> onEventDecoded) where TEventDTO : IEventDTO, new()
    {
        var eventSubscription = new EthLogsObservableSubscription(client);
        var eventFilterInput = Event<TEventDTO>.GetEventABI().CreateFilterInput(options.ContractAddress);

        eventSubscription.GetSubscriptionDataResponsesAsObservable().Subscribe(
            log =>
            {
                _logger.LogDebug(
                    $"[EvmRpcProvider] Network: {options.NetworkName} Block: {log.BlockHash}, BlockHeight: {log.BlockNumber}");
                try
                {
                    var decoded = Event<TEventDTO>.DecodeEvent(log);
                    if (decoded == null)
                    {
                        _logger.LogWarning(
                            $"[EvmRpcProvider] Network: {options.NetworkName} DecodeEvent failed at BlockHeight: {log.BlockNumber}");
                        return;
                    }

                    onEventDecoded(decoded);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"[EvmRpcProvider] Network: {options.NetworkName} Failed to decode event.");
                }
            },
            exception =>
                _logger.LogError(
                    $"[EvmRpcProvider] Network: {options.NetworkName} Subscription error: {exception.Message}")
        );

        _logger.LogDebug($"[EvmRpcProvider] Connecting WebSocket client on {options.NetworkName}...");
        await client.StartAsync();
        await eventSubscription.SubscribeAsync(eventFilterInput);
        _logger.LogDebug($"[EvmRpcProvider] Successfully subscribed to contract events on {options.NetworkName}.");
    }
}