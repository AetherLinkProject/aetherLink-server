using Nethereum.Contracts;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Threading.Tasks;
using Nethereum.RPC.Reactive.Eth;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Indexer.Provider
{
    public interface IBscRpcProvider
    {
        Task SubscribeAndRunAsync<TEventDTO>(Action<EventLog<TEventDTO>> onEventDecoded)
            where TEventDTO : IEventDTO, new();
    }

    public class BscRpcProvider : IBscRpcProvider, ISingletonDependency
    {
        private readonly EvmIndexerOptions _options;
        private readonly ILogger<BscRpcProvider> _logger;

        public BscRpcProvider(IOptionsSnapshot<EvmIndexerOptions> options, ILogger<BscRpcProvider> logger)
        {
            _logger = logger;
            _options = options.Value;
        }

        public async Task SubscribeAndRunAsync<TEventDTO>(Action<EventLog<TEventDTO>> onEventDecoded)
            where TEventDTO : IEventDTO, new()
        {
            using var client = new StreamingWebSocketClient(_options.WsUrl);

            try
            {
                await SubscribeToEventsAsync(client, onEventDecoded);
                
                while (true) 
                {
                    var handler = new EthBlockNumberObservableHandler(client);
                    handler.GetResponseAsObservable()
                        .Subscribe(x => _logger.LogDebug($"[BscRpcProvider] BlockHeight: {x.Value}"));
                    await handler.SendRequestAsync();
                    await Task.Delay(5000); 
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BscRpcProvider] An error occurred in the subscription process.");
                throw;
            }
            finally
            {
                await client.StopAsync();
                _logger.LogDebug("[BscRpcProvider] WebSocket client stopped.");
            }
        }
        
        private async Task SubscribeToEventsAsync<TEventDTO>(StreamingWebSocketClient client, 
            Action<EventLog<TEventDTO>> onEventDecoded, ulong fromBlock = 10000000) 
            where TEventDTO : IEventDTO, new()
        {
            var eventSubscription = new EthLogsObservableSubscription(client);

            _logger.LogDebug($"Start {_options.ContractAddress} subscription at block {fromBlock}");

            var eventFilterInput = Event<TEventDTO>.GetEventABI().CreateFilterInput(_options.ContractAddress);

            eventSubscription.GetSubscriptionDataResponsesAsObservable().Subscribe(
                log =>
                {
                    _logger.LogDebug($"[BscRpcProvider] New Block: {log.BlockHash} BlockHeight: {log.BlockNumber}");
                    try
                    {
                        var decoded = Event<TEventDTO>.DecodeEvent(log);
                        if (decoded == null)
                        {
                            _logger.LogWarning(
                                $"[BscRpcProvider] DecodeEvent failed at Block: {log.BlockHash}, BlockHeight: {log.BlockNumber}.");
                            return;
                        }
                        onEventDecoded(decoded);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "[BscRpcProvider] Failed to decode event.");
                    }
                },
                exception =>
                {
                    _logger.LogError($"[BscRpcProvider] Subscription error: {exception.Message}");
                });

            _logger.LogDebug("[BscRpcProvider] Connecting WebSocket client...");
            await client.StartAsync();
            await eventSubscription.SubscribeAsync(eventFilterInput);
            _logger.LogDebug("[BscRpcProvider] Successfully subscribed to contract events.");
        }
    }
}