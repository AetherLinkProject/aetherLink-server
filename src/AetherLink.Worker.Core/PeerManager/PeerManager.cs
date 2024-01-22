using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Options;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using AetherlinkClient = AetherLink.Worker.Core.AetherLinkServer.AetherLinkServerClient;

namespace AetherLink.Worker.Core.PeerManager;

public interface IPeerManager
{
    public int GetOwnIndex();
    public bool IsLeader(long epoch, int roundId);
    public Task BroadcastAsync<TResponse>(Func<AetherlinkClient, TResponse> func);
    public Task CommitToLeaderAsync<TResponse>(Func<AetherlinkClient, TResponse> func, long epoch, int roundId);
}

public class PeerManager : IPeerManager, ISingletonDependency
{
    private readonly int _peersCount;
    private readonly int _ownerIndex;
    private readonly NetworkOptions _option;
    private readonly ConcurrentDictionary<string, Connection> _peers = new();

    public PeerManager(IOptionsSnapshot<NetworkOptions> option)
    {
        _option = option.Value;
        _ownerIndex = _option.Index;
        _peersCount = _option.Domains.Count;

        var peerList = _option.Domains.ToList();
        peerList.RemoveAt(_ownerIndex);
        peerList.Where(domain => !string.IsNullOrEmpty(domain)).ToList()
            .ForEach(domain => _peers.TryAdd(domain, new Connection(domain)));
    }

    public int GetOwnIndex() => _ownerIndex;
    public bool IsLeader(long epoch, int roundId) => LeaderElection(epoch, roundId) == _ownerIndex;

    public async Task BroadcastAsync<TResponse>(Func<AetherlinkClient, TResponse> func)
        => await Task.WhenAll(_peers.Where(p => p.Value.IsConnectionReady())
            .Select(p => Task.FromResult(p.Value.CallAsync(func))));

    public Task CommitToLeaderAsync<TResponse>(Func<AetherlinkClient, TResponse> func, long epoch, int roundId)
        => Task.FromResult(_peers[_option.Domains[LeaderElection(epoch, roundId)]].CallAsync(func));

    private int LeaderElection(long epoch, int roundId) => (int)(epoch + roundId) % _peersCount;
    
    // todo: add meta insert
}