using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AetherLink.Worker.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using AetherlinkClient = AetherLink.Worker.Core.AetherLinkServer.AetherLinkServerClient;

namespace AetherLink.Worker.Core.PeerManager;

public interface IPeerManager
{
    public int GetOwnIndex();
    public int GetPeersCount();
    public bool IsLeader(long epoch, int roundId);
    public bool IsLeader(OCRContext context);
    public Task BroadcastAsync<TResponse>(Func<AetherlinkClient, TResponse> func);
    public Task CommitToLeaderAsync<TResponse>(Func<AetherlinkClient, TResponse> func, long epoch, int roundId);
    public Task CommitToLeaderAsync<TResponse>(Func<AetherlinkClient, TResponse> func, OCRContext context);
}

public class PeerManager : IPeerManager, ISingletonDependency
{
    private readonly int _peersCount;
    private readonly int _ownerIndex;
    private readonly NetworkOptions _option;
    private readonly ILogger<PeerManager> _logger;
    private readonly ConcurrentDictionary<string, Connection> _peers = new();

    public PeerManager(IOptionsSnapshot<NetworkOptions> option, ILogger<PeerManager> logger)
    {
        _logger = logger;
        _option = option.Value;
        _ownerIndex = _option.Index;
        _peersCount = _option.Domains.Count;
        InitConnection().GetAwaiter().GetResult();
    }

    public int GetOwnIndex() => _ownerIndex;
    public int GetPeersCount() => _peersCount;
    public bool IsLeader(long epoch, int roundId) => LeaderElection(epoch, roundId) == _ownerIndex;
    public bool IsLeader(OCRContext context) => LeaderElection(context.Epoch, context.RoundId) == _ownerIndex;

    public async Task BroadcastAsync<TResponse>(Func<AetherlinkClient, TResponse> func)
    {
        foreach (var peer in _peers)
        {
            await BroadcastHandleAsync(peer, func);
        }
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(PeerManager),
        MethodName = nameof(HandleBroadcastException))]
    public virtual async Task BroadcastHandleAsync<TResponse>(KeyValuePair<string, Connection> peer,
        Func<AetherlinkClient, TResponse> func)
    {
        if (await peer.Value.IsConnectionReady() == false)
        {
            _logger.LogWarning("[PeerManager] Peer {peer} connection is not ready", peer.Key);
            return;
        }

        _logger.LogDebug("[PeerManager] Send to peer {peer}", peer.Key);
        await Task.FromResult(peer.Value.CallAsync(func));
    }

    public async Task CommitToLeaderAsync<TResponse>(Func<AetherlinkClient, TResponse> func, long epoch,
        int roundId)
    {
        var leader = _option.Domains[LeaderElection(epoch, roundId)];

        await CommitToLeaderHandlerAsync(leader, func);
    }

    public async Task CommitToLeaderAsync<TResponse>(Func<AetherlinkClient, TResponse> func, OCRContext context)
    {
        var leader = _option.Domains[LeaderElection(context.Epoch, context.RoundId)];

        await CommitToLeaderHandlerAsync(leader, func);
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(PeerManager),
        MethodName = nameof(HandleCommitToLeaderException))]
    public virtual async Task CommitToLeaderHandlerAsync<TResponse>(string leader,
        Func<AetherlinkClient, TResponse> func)
    {
        _logger.LogDebug("[PeerManager] Send to leader {peer}", leader);
        await Task.FromResult(_peers[leader].CallAsync(func));
    }

    private async Task InitConnection()
    {
        var peerList = _option.Domains.ToList();
        peerList.RemoveAt(_ownerIndex);
        peerList.Where(domain => !string.IsNullOrEmpty(domain)).ToList()
            .ForEach(async domain => { await InitConnectionHandle(domain); });
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(PeerManager),
        MethodName = nameof(HandleInitConnectionException))]
    public virtual async Task InitConnectionHandle(string domain)
    {
        _peers.TryAdd(domain, new Connection(domain));
    }

    private int LeaderElection(long epoch, int roundId) => (int)(epoch + roundId) % _peersCount;

    // todo: add meta insert

    #region Exception Handing

    public async Task<FlowBehavior> HandleBroadcastException(Exception ex, KeyValuePair<string, Connection> peer)
    {
        if (ex is OperationCanceledException)
        {
            // todo: add retry maybe
            _logger.LogError("[PeerManager] Peer {peer} request is canceled.", peer.Key);
        }
        else
        {
            _logger.LogError(ex, "[PeerManager] Peer {peer} request failed.", peer.Key);
        }

        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
        };
    }

    public async Task<FlowBehavior> HandleCommitToLeaderException(Exception ex, string leader)
    {
        _logger.LogError(ex, "[PeerManager] Send to leader {peer} failed.", leader);
        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return
        };
    }

    public async Task<FlowBehavior> HandleInitConnectionException(Exception ex, string domain)
    {
        _logger.LogWarning(ex, "[PeerManager] Init {domain} client failed, please check address", domain);
        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return
        };
    }

    #endregion
}