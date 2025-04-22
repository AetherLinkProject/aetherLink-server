using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Constants;
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
    public int GetCurrentRoundId(long startTime);
    public int GetCurrentRoundId(DateTime startTime);
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
        InitConnection();
    }

    public int GetOwnIndex() => _ownerIndex;
    public int GetPeersCount() => _peersCount;
    public bool IsLeader(long epoch, int roundId) => LeaderElection(epoch, roundId) == _ownerIndex;
    public bool IsLeader(OCRContext context) => LeaderElection(context.Epoch, context.RoundId) == _ownerIndex;

    public int GetCurrentRoundId(long startTime)
    {
        var unixCurrentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        return (int)(unixCurrentTime - startTime / RequestProgressConstants.CheckRequestEndTimeoutWindow) + 1;
    }

    public int GetCurrentRoundId(DateTime startTime)
    {
        var unixCurrentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        var unixStartTime = new DateTimeOffset(startTime).ToUnixTimeMilliseconds();
        return (int)(unixCurrentTime - unixStartTime / RequestProgressConstants.CheckRequestEndTimeoutWindow) + 1;
    }

    public async Task BroadcastAsync<TResponse>(Func<AetherlinkClient, TResponse> func)
    {
        foreach (var peer in _peers)
        {
            try
            {
                if (!peer.Value.IsConnectionReady())
                {
                    _logger.LogWarning("[PeerManager] Peer {peer} connection is not ready", peer.Key);
                    return;
                }

                _logger.LogDebug("[PeerManager] Send to peer {peer}", peer.Key);
                await Task.FromResult(peer.Value.CallAsync(func));
            }
            catch (OperationCanceledException)
            {
                // todo: add retry maybe
                _logger.LogError("[PeerManager] Peer {peer} request is canceled.", peer.Key);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[PeerManager] Peer {peer} request failed.", peer.Key);
            }
        }
    }

    public async Task CommitToLeaderAsync<TResponse>(Func<AetherlinkClient, TResponse> func, long epoch,
        int roundId)
    {
        var leader = _option.Domains[LeaderElection(epoch, roundId)];
        try
        {
            _logger.LogDebug("[PeerManager] Send to leader {peer}", leader);
            await Task.FromResult(_peers[leader].CallAsync(func));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[PeerManager] Send to leader {peer} failed.", leader);
        }
    }

    public async Task CommitToLeaderAsync<TResponse>(Func<AetherlinkClient, TResponse> func, OCRContext context)
    {
        var leader = _option.Domains[LeaderElection(context.Epoch, context.RoundId)];
        try
        {
            _logger.LogDebug("[PeerManager] Send to leader {peer}", leader);
            await Task.FromResult(_peers[leader].CallAsync(func));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[PeerManager] Send to leader {peer} failed.", leader);
        }
    }

    private void InitConnection()
    {
        var peerList = _option.Domains.ToList();
        peerList.RemoveAt(_ownerIndex);
        peerList.Where(domain => !string.IsNullOrEmpty(domain)).ToList()
            .ForEach(domain =>
            {
                try
                {
                    _peers.TryAdd(domain, new Connection(domain));
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "[PeerManager] Init {domain} client failed, please check address", domain);
                }
            });
    }

    private int LeaderElection(long epoch, int roundId) => (int)(epoch + roundId) % _peersCount;

    // todo: add meta insert
}