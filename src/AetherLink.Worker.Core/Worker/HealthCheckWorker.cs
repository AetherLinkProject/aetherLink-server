using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.PeerManager;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherLink.Worker.Core.Worker;

public class HealthCheckWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly WorkerOptions _options;
    private readonly IPeerManager _peerManager;
    private readonly ILogger<HealthCheckWorker> _logger;
    private readonly ConcurrentDictionary<string, int> _peerRetryTimesMap = new();

    public HealthCheckWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory, IPeerManager peerManager,
        ILogger<HealthCheckWorker> logger, IOptions<WorkerOptions> options) : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _peerManager = peerManager;
        Timer.Period = 1000 * _options.HealthCheckTimer;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.LogDebug("[HealthCheckWorker] Starting health check.");
        var tasks = _peerManager.GetPeers().Values.Select(HealthCheckAsync);
        await Task.WhenAll(tasks);
    }

    private async Task HealthCheckAsync(Peer peer)
    {
        if (peer.HealthCheck())
        {
            _logger.LogDebug("[HealthCheckWorker] {ep} is health.", peer.Domain);
            _peerManager.UpdatePeerStatue(peer.Domain, PeerState.Ready);
            return;
        }

        _logger.LogWarning("[HealthCheckWorker] {ep} is not health.", peer.Domain);

        if (await peer.ConnectAsync())
        {
            _logger.LogDebug("[HealthCheckWorker] Peer {ep} reconnect success", peer.Domain);
            _peerRetryTimesMap.TryRemove(peer.Domain, out _);
            _peerManager.UpdatePeerStatue(peer.Domain, PeerState.Ready);
            return;
        }

        _logger.LogWarning("[HealthCheckWorker] Peer {ep} reconnect fail", peer.Domain);
        var retryTimes = _peerRetryTimesMap.AddOrUpdate(peer.Domain, 1, (_, v) => v + 1);
        _peerManager.UpdatePeerStatue(peer.Domain,
            retryTimes > _options.HealthCheckMaxRetryTimes ? PeerState.Removed : PeerState.Connecting);
    }
}