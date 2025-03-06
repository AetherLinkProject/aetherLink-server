using AElf;
using AetherLink.Indexer;
using AetherLink.Indexer.Dtos;
using AetherLink.Server.Grains.Grain.Request;
using AetherLink.Server.HttpApi.Dtos;
using AetherLink.Server.HttpApi.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.RPC.Reactive.Eth;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherLink.Server.HttpApi.Worker.Evm;

public class EvmSearchWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly EVMOptions _options;
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<EvmSearchWorker> _logger;
    private readonly EvmIndexerOptionsMap _networkOptions;
    private readonly Dictionary<string, StreamingWebSocketClient> _clients = new();

    public EvmSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<EVMOptions> options, IClusterClient clusterClient, ILogger<EvmSearchWorker> logger,
        IOptionsSnapshot<EvmIndexerOptionsMap> networkOptions) : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _clusterClient = clusterClient;
        _networkOptions = networkOptions.Value;
        timer.Period = _options.TransactionSearchTimer;

        foreach (var (networkKey, networkConfig) in _networkOptions.ChainInfos)
        {
            _clients[networkKey] = new StreamingWebSocketClient(networkConfig.WsUrl);
        }
    }

    private async Task InitializeWebSocketAsync(string networkKey, EvmIndexerOptions options)
    {
        if (_clients.TryGetValue(networkKey, out var existingClient) && existingClient.IsStarted)
        {
            _logger.LogInformation($"[{networkKey}] WebSocket already running, skipping initialization.");
            return;
        }

        _logger.LogInformation($"[{networkKey}] Initializing WebSocket...");

        var client = new StreamingWebSocketClient(options.WsUrl);
        _clients[networkKey] = client;

        await client.StartAsync();
        await SubscribeToEventsAsync<SendEventDTO>(networkKey, client, options, eventData =>
        {
            _logger.LogInformation("[EvmSearchServer] Received send event --> ");
            HandleRequestStartAsync(eventData);
        });
        await SubscribeToEventsAsync<ForwardMessageCalledEventDTO>(networkKey, client, options, eventData =>
        {
            _logger.LogInformation("[EvmSearchServer] Received commit event --> ");
            HandleCommittedAsync(eventData);
        });

        _logger.LogInformation($"[{networkKey}] WebSocket connection initialize.");
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        foreach (var kv in _clients)
        {
            var networkKey = kv.Key;
            var client = kv.Value;

            if (!client.IsStarted)
            {
                _logger.LogWarning($"[{networkKey}] WebSocket is not started, connecting or reconnecting...");
                await InitializeWebSocketAsync(networkKey, _networkOptions.ChainInfos[networkKey]);
                continue;
            }

            try
            {
                _logger.LogInformation($"[{networkKey}] Sending Ping...");

                var handler = new EthBlockNumberObservableHandler(client);
                handler.GetResponseAsObservable().Subscribe(blockNumber =>
                    _logger.LogInformation($"[{networkKey}] Block Height: {blockNumber.Value}")
                );

                await handler.SendRequestAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[{networkKey}] Ping failed, attempting reconnection...");
                await InitializeWebSocketAsync(networkKey, _networkOptions.ChainInfos[networkKey]);
            }
        }
    }

    private async Task SubscribeToEventsAsync<TEventDTO>(string networkKey, StreamingWebSocketClient client,
        EvmIndexerOptions options, Action<EventLog<TEventDTO>> onEventDecoded) where TEventDTO : IEventDTO, new()
    {
        var eventSubscription = new EthLogsObservableSubscription(client);
        var eventFilterInput = Event<TEventDTO>.GetEventABI().CreateFilterInput(options.ContractAddress);

        eventSubscription.GetSubscriptionDataResponsesAsObservable()
            .Subscribe(log =>
            {
                _logger.LogInformation($"[{networkKey}] Event Received - Block {log.BlockNumber}");
                try
                {
                    var decoded = Event<TEventDTO>.DecodeEvent(log);
                    if (decoded != null) onEventDecoded(decoded);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"[{networkKey}] Failed to decode event.");
                }
            });

        await eventSubscription.SubscribeAsync(eventFilterInput);
        _logger.LogInformation($"[{networkKey}] Subscribed to contract events.");
    }

    private async Task HandleRequestStartAsync(EventLog<SendEventDTO> eventData)
    {
        var sendRequestData = eventData.Event;
        var messageId = sendRequestData.MessageId.ToHex();
        var requestGrain = _clusterClient.GetGrain<ICrossChainRequestGrain>(messageId);
        var result = await requestGrain.UpdateAsync(new()
        {
            Id = messageId,
            SourceChainId = (long)sendRequestData.SourceChainId,
            TargetChainId = (long)sendRequestData.TargetChainId,
            MessageId = messageId,
            Status = CrossChainStatus.Started.ToString()
        });

        _logger.LogDebug($"[CommitSearchWorker] Create {messageId} started {result.Success}");
    }

    private async Task HandleCommittedAsync(EventLog<ForwardMessageCalledEventDTO> eventData)
    {
        var transmitEvent = eventData.Event;
        var messageId = transmitEvent.MessageId.ToHex();
        var requestGrain = _clusterClient.GetGrain<ICrossChainRequestGrain>(messageId);
        var result = await requestGrain.UpdateAsync(new()
        {
            Id = messageId,
            SourceChainId = (long)transmitEvent.SourceChainId,
            TargetChainId = (long)transmitEvent.TargetChainId,
            MessageId = messageId,
            Status = CrossChainStatus.Committed.ToString()
        });

        _logger.LogDebug($"[CommitSearchWorker] Update {messageId} committed {result.Success}");
    }
}