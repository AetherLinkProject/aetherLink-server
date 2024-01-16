using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace AetherLink.Worker.Core.Network;

public class Connector
{
    private Channel _channel;
    private readonly string Endpoint;
    private AetherLinkService.AetherLinkServiceClient _client;
    private readonly ConcurrentQueue<StreamMessage> _retryQueue = new();
    private static ILogger<Connector> _logger = new Logger<Connector>(new LoggerFactory());
    private AsyncClientStreamingCall<StreamMessage, VoidReply> _streamCall;

    public Connector(string endpoint)
    {
        Endpoint = endpoint;
        InitConnectorAsync(Endpoint);
    }

    private async void InitConnectorAsync(string dnsEndPoint)
    {
        try
        {
            _channel = new Channel(dnsEndPoint, ChannelCredentials.Insecure, new List<ChannelOption>());
            _client = new AetherLinkService.AetherLinkServiceClient(_channel);

            await _channel.ConnectAsync();
            _logger.LogInformation("[Connector] Peer {ep} connect success.", Endpoint);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Connector] Peer {ep} connect fail. ", Endpoint);
        }
    }

    public bool IsConnectionHealth()
    {
        return _channel.State == ChannelState.Ready;
    }

    public async Task RequestAsync(StreamMessage streamMessage)
    {
        try
        {
            _streamCall ??= _client.RequestStreamAsync();
            await _client.RequestStreamAsync().RequestStream.WriteAsync(streamMessage);
        }
        catch (RpcException e)
        {
            _logger.LogError(e, "[Connector] Request failed. peer:{ep}, type:{type}", Endpoint,
                streamMessage.MessageType);
            _retryQueue.Enqueue(streamMessage);
        }
    }

    public async Task<bool> ConnectAsync()
    {
        try
        {
            await _channel.ConnectAsync();
            _ = Task.Run(Replay);
            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Connector] Peer {ep} connect fail.", Endpoint);
            return false;
        }
    }

    private async void Replay()
    {
        for (var i = _retryQueue.Count - 1; i >= 0; i--)
        {
            var dequeue = _retryQueue.TryDequeue(out var streamMessage);
            if (dequeue)
            {
                await RequestAsync(streamMessage);
            }
        }
    }
}