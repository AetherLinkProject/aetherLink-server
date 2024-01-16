using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Network;
using Serilog.Core;

namespace AetherLink.Worker.Core.PeerManager;

public class Peer
{
    public readonly string Domain;
    public PeerState State;
    private readonly Connector _connector;

    public Peer(string domain)
    {
        Domain = domain;
        State = PeerState.Connecting;
        _connector = new Connector(Domain);
    }

    public async Task RequestAsync(StreamMessage streamMessage)
    {
        await _connector.RequestAsync(streamMessage);
    }

    public bool HealthCheck()
    {
        return _connector.IsConnectionHealth();
    }

    public async Task<bool> ConnectAsync()
    {
        return await _connector.ConnectAsync();
    }
}