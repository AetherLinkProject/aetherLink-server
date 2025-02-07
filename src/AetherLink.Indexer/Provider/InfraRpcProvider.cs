using Nethereum.Contracts;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.RPC.Reactive.Eth;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Indexer.Provider;

public interface IInfuraRpcProvider
{
    public Task SubscribeAndRunAsync<TEventDTO>(Action<TEventDTO> onEventDecoded) where TEventDTO : IEventDTO, new();
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

    public async Task SubscribeAndRunAsync<TEventDTO>(Action<TEventDTO> onEventDecoded)
        where TEventDTO : IEventDTO, new()
    {
        var client = new StreamingWebSocketClient(_options.WsUrl);
        var eventSubscription = new EthLogsObservableSubscription(client);

        var eventFilterInput = Event<TEventDTO>.GetEventABI().CreateFilterInput(_options.ContractAddress);
        eventSubscription.GetSubscriptionDataResponsesAsObservable().Subscribe(log =>
            {
                _logger.LogDebug($"[InfuraRpcProvider] New Block: {log.BlockHash} BlockHeight: {log.BlockNumber}");

                try
                {
                    var decoded = Event<TEventDTO>.DecodeEvent(log);
                    if (decoded == null)
                    {
                        _logger.LogWarning(
                            $"[InfuraRpcProvider] DecodeEvent failed at Block: {log.BlockHash} BlockHeight: {log.BlockNumber}.");
                    }
                    else
                    {
                        onEventDecoded(decoded.Event);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "[InfuraRpcProvider] DecodeEvent failed.");
                }
            },
            exception =>
            {
                _logger.LogError("[InfuraRpcProvider] BlockHeaderSubscription error info:" + exception.Message);
            });

        await client.StartAsync();
        await eventSubscription.SubscribeAsync(eventFilterInput);

        Console.WriteLine("[InfuraRpcProvider] Subscribed to the given contract events successfully!");

        while (true) //pinging to keep alive infura
        {
            var handler = new EthBlockNumberObservableHandler(client);
            handler.GetResponseAsObservable().Subscribe(x => _logger.LogDebug($"[InfuraRpcProvider] {x.Value}"));
            await handler.SendRequestAsync();
            Thread.Sleep(30000);
        }
    }
}