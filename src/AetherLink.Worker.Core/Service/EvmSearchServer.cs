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
    private readonly ConcurrentDictionary<string, long> _heightMap = new();

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
        if (_heightMap.TryGetValue(indexerOptions.NetworkName, out var latestConsumedBlockHeight) ||
            latestConsumedBlockHeight != 0)
        {
            await DealingWithMissedDataAsync(indexerOptions);
        }

        await StartSubscribeAndRunAsync(indexerOptions);
    }

    private async Task StartSubscribeAndRunAsync(EvmOptions networkOptions)
    {
        _logger.LogInformation($"[EvmRpcProvider] Starting subscription on Network: {networkOptions.NetworkName}");

        // initial retry time == 1 min
        var retryInterval = 1000;

        while (true)
        {
            using var client = new StreamingWebSocketClient(networkOptions.WsUrl);
            try
            {
                await SubscribeToEventsAsync(client, networkOptions);

                while (true)
                {
                    var handler = new EthBlockNumberObservableHandler(client);
                    handler.GetResponseAsObservable().Subscribe(async x =>
                    {
                        await SaveConsumedBlockHeightAsync(networkOptions.NetworkName, (long)x.Value);
                        _logger.LogDebug(
                            $"[EvmRpcProvider] Network: {networkOptions.NetworkName} BlockHeight: {x.Value}");
                    });

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

                await DealingWithMissedDataAsync(networkOptions);

                _logger.LogDebug(
                    $"[EvmRpcProvider] Network: {networkOptions.NetworkName} WebSocket client stopped. Restarting...");
            }
        }
    }

    private async Task SubscribeToEventsAsync(StreamingWebSocketClient client, EvmOptions options)
    {
        var eventSubscription = new EthLogsObservableSubscription(client);
        var eventFilterInput = Event<SendEventDTO>.GetEventABI().CreateFilterInput(options.ContractAddress);

        eventSubscription.GetSubscriptionDataResponsesAsObservable().Subscribe(
            async log =>
            {
                await DecodeAndStartCrossChainAsync(log);
                await SaveConsumedBlockHeightAsync(options.NetworkName, (long)log.BlockNumber.Value);

                _logger.LogDebug(
                    $"[EvmRpcProvider] Network: {options.NetworkName} Block: {log.BlockHash}, BlockHeight: {log.BlockNumber}");
            },
            exception => _logger.LogError(
                $"[EvmRpcProvider] Network: {options.NetworkName} Subscription error: {exception.Message}")
        );

        _logger.LogDebug($"[EvmRpcProvider] Connecting WebSocket client on {options.NetworkName}...");

        await client.StartAsync();
        await eventSubscription.SubscribeAsync(eventFilterInput);

        _logger.LogDebug($"[EvmRpcProvider] Successfully subscribed to contract events on {options.NetworkName}.");
    }

    private async Task DealingWithMissedDataAsync(EvmOptions options)
    {
        var lastProcessedBlock = _heightMap[options.NetworkName];
        _logger.LogInformation($"[EvmSearchServer] Last processed block: {lastProcessedBlock}");
        if (lastProcessedBlock == 0)
        {
            _logger.LogInformation("[EvmSearchServer] There is no needs to be consumed by https.");
            return;
        }

        var web3 = new Web3(options.Api);
        var latestBlock = (long)(await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value;
        _logger.LogInformation($"[EvmSearchServer] Current latest block: {latestBlock}");

        if (lastProcessedBlock == latestBlock)
        {
            _logger.LogInformation("[EvmSearchServer] There is no missing height that needs to be consumed.");
            return;
        }

        var from = lastProcessedBlock + 1;
        var to = latestBlock - 1;
        for (var currentFrom = from; currentFrom <= to; currentFrom += EvmSubscribeConstants.SubscribeBlockStep)
        {
            var currentTo = Math.Min(currentFrom + EvmSubscribeConstants.SubscribeBlockStep - 1, to);
            var filterInput = new NewFilterInput
            {
                FromBlock = new BlockParameter((ulong)currentFrom),
                ToBlock = new BlockParameter((ulong)currentTo),
                Address = new[] { options.ContractAddress }
            };
            var logs = await web3.Eth.Filters.GetLogs.SendRequestAsync(filterInput);
            var tasks = logs.Select(DecodeAndStartCrossChainAsync);

            await Task.WhenAll(tasks);
        }
    }

    private async Task DecodeAndStartCrossChainAsync(FilterLog log)
    {
        try
        {
            var decoded = Event<SendEventDTO>.DecodeEvent(log);
            if (decoded == null)
            {
                _logger.LogWarning($"[EvmRpcProvider] Failed to decode event at: {log.BlockNumber}");
                return;
            }

            var messagePendingToCrossChain = GenerateEvmReceivedMessage(decoded);
            await _crossChainProvider.StartCrossChainRequestFromEvm(messagePendingToCrossChain);

            _logger.LogDebug(
                $"[EvmRpcProvider] Start cross chain request {messagePendingToCrossChain.MessageId} successful at: {log.TransactionHash} {log.BlockNumber}");
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[EvmRpcProvider] Decode {log.TransactionHash} fail at {log.BlockNumber}.");
        }
    }

    private async Task InitializeConsumedHeight(EvmContractsOptions options)
    {
        _logger.LogDebug("[EvmRpcProvider] Start consumption height setting");

        foreach (var op in options.ContractConfig.Values)
        {
            var redisKey = IdGeneratorHelper.GenerateId(RedisKeyConstants.SearchHeightKey, op.NetworkName);
            var latestBlockHeight = await _storageProvider.GetAsync<SearchHeightDto>(redisKey);

            _logger.LogDebug($"[EvmRpcProvider] {op.NetworkName} has consumed {latestBlockHeight} with {redisKey}.");

            _heightMap[op.NetworkName] = latestBlockHeight?.BlockHeight ?? 0;
        }
    }

    private async Task SaveConsumedBlockHeightAsync(string network, long blockHeight)
    {
        var redisKey = IdGeneratorHelper.GenerateId(RedisKeyConstants.SearchHeightKey, network);
        await _storageProvider.SetAsync(redisKey, new SearchHeightDto { BlockHeight = blockHeight });

        _heightMap[network] = blockHeight;

        _logger.LogDebug($"[EvmRpcProvider] Network {network} has consumed to height {blockHeight} with {redisKey}.");
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