using Nethereum.Contracts;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using System;
using System.Threading.Tasks;
using AetherLink.Indexer.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.RPC.Reactive.Eth;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Indexer.Provider;

public interface IInfuraRpcProvider
{
    Task SubscribeAndRunAsync<TEventDTO>(Action<EventLog<TEventDTO>> onEventDecoded)
        where TEventDTO : IEventDTO, new();
}

public class InfuraRpcProvider : IInfuraRpcProvider, ISingletonDependency
{
    private readonly EvmIndexerOptions _options;
    private readonly ILogger<InfuraRpcProvider> _logger;

    public InfuraRpcProvider(IOptionsSnapshot<EvmIndexerOptions> options, ILogger<InfuraRpcProvider> logger)
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
            await KeepClientAliveAsync(client);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[InfuraRpcProvider] An error occurred in the subscription process.");
            throw;
        }
        finally
        {
            await client.StopAsync();
            _logger.LogDebug("[InfuraRpcProvider] WebSocket client stopped.");
        }
    }

    private async Task SubscribeToEventsAsync<TEventDTO>(StreamingWebSocketClient client,
        Action<EventLog<TEventDTO>> onEventDecoded)
        where TEventDTO : IEventDTO, new()
    {
        var eventSubscription = new EthLogsObservableSubscription(client);
        var eventFilterInput = Event<TEventDTO>.GetEventABI().CreateFilterInput(_options.ContractAddress);

        eventSubscription.GetSubscriptionDataResponsesAsObservable().Subscribe(
            log =>
            {
                _logger.LogDebug($"[InfuraRpcProvider] New Block: {log.BlockHash} BlockHeight: {log.BlockNumber}");

                try
                {
                    var decoded = Event<TEventDTO>.DecodeEvent(log);
                    if (decoded == null)
                    {
                        _logger.LogWarning(
                            $"[InfuraRpcProvider] DecodeEvent failed at Block: {log.BlockHash}, BlockHeight: {log.BlockNumber}.");
                        return;
                    }

                    onEventDecoded(decoded);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "[InfuraRpcProvider] Failed to decode event.");
                }
            },
            exception => { _logger.LogError($"[InfuraRpcProvider] Subscription error: {exception.Message}"); });

        _logger.LogDebug("[InfuraRpcProvider] Connecting WebSocket client...");
        await client.StartAsync();
        await eventSubscription.SubscribeAsync(eventFilterInput);

        _logger.LogDebug("[InfuraRpcProvider] Successfully subscribed to contract events.");
    }

    private async Task KeepClientAliveAsync(StreamingWebSocketClient client)
    {
        var blockNumberHandler = new EthBlockNumberObservableHandler(client);

        blockNumberHandler
            .GetResponseAsObservable()
            .Subscribe(blockNumber =>
                _logger.LogDebug($"[InfuraRpcProvider] Current BlockHeight: {blockNumber.Value}"));

        while (client.IsStarted)
        {
            try
            {
                await blockNumberHandler.SendRequestAsync();
                await Task.Delay(NetworkConstants.DefaultEvmApiDelay); // 使用延迟代替阻塞线程
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[InfuraRpcProvider] Keep-alive process encountered an error.");
                break;
            }
        }

        _logger.LogWarning("[InfuraRpcProvider] WebSocket client is no longer running.");
    }
}