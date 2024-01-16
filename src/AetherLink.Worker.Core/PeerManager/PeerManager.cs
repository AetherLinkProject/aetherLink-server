using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AElf.Types;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.PeerManager;

public interface IPeerManager
{
    public Task BroadcastRequestAsync(StreamMessage streamMessage);
    public Task RequestLeaderAsync(StreamMessage streamMessage, string chainId, int roundId);
    public Task<bool> IsLeaderAsync(string chainId, int roundId);
    public Dictionary<string, Peer> GetPeers();
    public void UpdatePeerStatue(string domain, PeerState peerState);
    public int GetOwnIndex();
    public Task<long> GetEpochAsync(string chainId);
    public void UpdateEpoch(string chainId, long epoch);
    public Task<Hash> GetLatestConfigDigestAsync(string chainId);
}

public class PeerManager : IPeerManager, ISingletonDependency
{
    private readonly NetworkOptions _option;
    private readonly List<string> _localIpList;
    private readonly ILogger<PeerManager> _logger;
    private readonly ConcurrentDictionary<string, Peer> _peers;
    private readonly ConcurrentDictionary<string, long> _epochDict;
    private readonly ConcurrentDictionary<string, bool> _domainDict;
    private readonly IOracleContractProvider _oracleContractProvider;
    private readonly ConcurrentDictionary<string, string> _oracleConfigDict;

    public PeerManager(IOptionsSnapshot<NetworkOptions> option, ILogger<PeerManager> logger,
        IOracleContractProvider oracleContractProvider)
    {
        _logger = logger;
        _option = option.Value;
        _localIpList = Dns.GetHostEntry(Dns.GetHostName()).AddressList.Select(ip => ip + ":" + _option.ListenPort)
            .ToList();
        _peers = new();
        _epochDict = new();
        _domainDict = new();
        _oracleConfigDict = new();
        _oracleContractProvider = oracleContractProvider;
        InitPeersAsync();
    }

    private void InitPeersAsync()
    {
        var peers = _option.Domains
            .Select(domain => new Peer(domain))
            .Where(peer => !IsLocal(peer.Domain)).ToList();

        foreach (var peer in peers) _peers.TryAdd(peer.Domain, peer);

        _logger.LogInformation("[PeerManager] Init finished with {count} peers", _peers.Count);
    }

    public async Task BroadcastRequestAsync(StreamMessage streamMessage)
    {
        var peers = GetPeers().Select(peer => peer.Value).ToList();
        streamMessage.StreamType = StreamType.Request;
        foreach (var p in peers)
        {
            _logger.LogInformation("[BroadcastRequest] send {type} to {domain}", streamMessage.MessageType, p.Domain);
            await p.RequestAsync(streamMessage);
        }
    }

    public async Task<bool> IsLeaderAsync(string chainId, int roundId)
    {
        var leaderDomain = LeaderElection(await GetEpochAsync(chainId), roundId);
        return IsLocal(leaderDomain);
    }

    public async Task RequestLeaderAsync(StreamMessage streamMessage, string chainId, int roundId)
    {
        var leaderDomain = LeaderElection(await GetEpochAsync(chainId), roundId);

        if (!_peers.TryGetValue(leaderDomain, out var leader))
        {
            _logger.LogWarning("Can't find leader.");
            return;
        }

        streamMessage.StreamType = StreamType.Request;
        await leader.RequestAsync(streamMessage);
    }

    public Dictionary<string, Peer> GetPeers()
    {
        return new Dictionary<string, Peer>(
            _peers.Where(p => p.Value.State is PeerState.Ready or PeerState.Connecting));
    }

    public void UpdatePeerStatue(string domain, PeerState peerState)
    {
        if (_peers.TryGetValue(domain, out var peer))
        {
            peer.State = peerState;
            _peers.AddOrUpdate(domain, peer, (_, _) => peer);
        }
    }

    public int GetOwnIndex()
    {
        var ownIndex = _option.Domains
            .Select((domain, idx) => new { Domain = domain, Index = idx })
            .FirstOrDefault(x => IsLocal(x.Domain))!.Index;
        _logger.LogDebug("OwnIndex: {index} ", ownIndex);
        return ownIndex;
    }

    public async Task<long> GetEpochAsync(string chainId)
    {
        if (_epochDict.TryGetValue(chainId, out var epoch))
        {
            _logger.LogDebug("[GetEpoch] ChainId: {chain} Epoch:{epoch}", chainId, epoch);
            return epoch;
        }

        var latestRound = await _oracleContractProvider.GetLatestRoundAsync(chainId);
        _epochDict[chainId] = latestRound;
        return latestRound;
    }

    public void UpdateEpoch(string chainId, long epoch)
    {
        _epochDict[chainId] = epoch;
    }

    public async Task<Hash> GetLatestConfigDigestAsync(string chainId)
    {
        if (_oracleConfigDict.TryGetValue(chainId, out var oracleConfig))
        {
            _logger.LogDebug("[LatestConfigDigest] ChainId: {chain} Config:{Config}", chainId, oracleConfig);
            return Hash.LoadFromHex(oracleConfig);
        }

        var configDigest = await _oracleContractProvider.GetOracleConfigAsync(chainId);
        _oracleConfigDict[chainId] = configDigest.ToHex();
        return configDigest;
    }

    private bool IsLocal(string domain)
    {
        _logger.LogDebug("Local domain: {domain} ", domain);

        if (string.IsNullOrEmpty(domain)) return false;
        if (_domainDict.TryGetValue(domain, out var cache)) return cache;

        var parts = domain.Split(':');
        var domainName = parts[0];
        var port = parts.Length > 1 ? parts[1] : "";

        var hostIp = IPAddress.TryParse(domainName, out var address)
            ? new IPHostEntry { AddressList = new[] { address } }
            : Dns.GetHostEntry(domainName);
        var host = hostIp.AddressList.Select(ip => ip + ":" + port).ToList();

        var isLocal = host.Any(ip => _localIpList.Contains(ip));
        _domainDict.TryAdd(domain, isLocal);
        return isLocal;
    }

    private string LeaderElection(long epoch, int roundId)
    {
        var leaderIndex = (epoch + roundId) % _option.Domains.Count;
        _logger.LogDebug("Leader election leaderIndex: {i}", leaderIndex);
        // todo: select without options
        return _option.Domains[(int)leaderIndex];
    }
}