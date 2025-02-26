using Nethereum.Contracts;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Reactive.Eth;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Indexer.Provider
{
    public interface IEvmRpcProvider
    {
        Task SubscribeAndRunAsync<TEventDTO>(EvmIndexerOptions networkOptions,
            Action<EventLog<TEventDTO>> onEventDecoded, ulong fromBlock = 0) where TEventDTO : IEventDTO, new();
    }

    public class EvmRpcProvider : IEvmRpcProvider, ISingletonDependency
    {
        private readonly ILogger<EvmRpcProvider> _logger;

        public EvmRpcProvider(ILogger<EvmRpcProvider> logger)
        {
            _logger = logger;
        }

        public async Task SubscribeAndRunAsync<TEventDTO>(EvmIndexerOptions networkOptions,
            Action<EventLog<TEventDTO>> onEventDecoded, ulong fromBlock = 0) where TEventDTO : IEventDTO, new()
        {
            _logger.LogInformation($"[EvmRpcProvider] Starting subscription on Network: {networkOptions.NetworkName}");

            // initial retry time == 1min
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
                        handler.GetResponseAsObservable()
                            .Subscribe(x =>
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

        private async Task SubscribeToEventsAsync<TEventDTO>(StreamingWebSocketClient client, EvmIndexerOptions options,
            Action<EventLog<TEventDTO>> onEventDecoded, ulong fromBlock = 0) where TEventDTO : IEventDTO, new()
        {
            var eventSubscription = new EthLogsObservableSubscription(client);
            var eventFilterInput = Event<TEventDTO>.GetEventABI().CreateFilterInput(options.ContractAddress);

            if (fromBlock != 0)
            {
                eventFilterInput = Event<TEventDTO>.GetEventABI().CreateFilterInput(
                    new BlockParameter(new HexBigInteger(fromBlock)),
                    options.ContractAddress);
            }

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
}