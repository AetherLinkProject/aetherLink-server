using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Automation.Args;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.PeerManager;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.Automation.Providers;

public class AutomationTimerProvider : ISingletonDependency
{
    private readonly IPeerManager _peerManager;
    private readonly IJobProvider _jobProvider;
    private readonly IObjectMapper _objectMapper;
    private readonly IBackgroundJobManager _jobManager;
    private readonly ILogger<AutomationTimerProvider> _logger;
    private readonly ConcurrentDictionary<string, long> _epochDict = new();

    public AutomationTimerProvider(IBackgroundJobManager jobManager, ILogger<AutomationTimerProvider> logger,
        IObjectMapper objectMapper, IJobProvider jobProvider, IPeerManager peerManager)
    {
        _logger = logger;
        _jobManager = jobManager;
        _jobProvider = jobProvider;
        _peerManager = peerManager;
        _objectMapper = objectMapper;
    }

    public async Task ExecuteAsync(AutomationJobArgs args)
    {
        var reqId = args.RequestId;
        var chainId = args.ChainId;
        var argId = IdGeneratorHelper.GenerateId(chainId, reqId);
        _logger.LogInformation("[AutomationTimer] {name} Execute checking.", argId);

        var request = await _jobProvider.GetAsync(args);
        if (request == null)
        {
            // only new request will update epochDict from args Epoch, others will updated epoch by request epoch from transmitted logevent
            _epochDict[argId] = args.Epoch;
            await _jobManager.EnqueueAsync(_objectMapper.Map<AutomationJobArgs, AutomationStartJobArgs>(args));
            return;
        }

        var requestStartArgs = _objectMapper.Map<AutomationJobArgs, AutomationStartJobArgs>(args);
        _epochDict.TryGetValue(argId, out var epoch);
        if (request.Epoch == epoch && request.Epoch != 0)
        {
            var newRoundId = _peerManager.GetCurrentRoundId(request.RequestReceiveTime);
            if (newRoundId <= request.RoundId)
            {
                _logger.LogDebug("[AutomationTimer] The last round {Epoch} wasn't finished. reqId {reqId}",
                    request.RoundId, reqId);
                return;
            }

            _logger.LogInformation("[AutomationTimer] {reqId} New round will updated, {or} => {ne}", reqId,
                request.RoundId, newRoundId);
            requestStartArgs.RoundId = newRoundId;
        }
        else
        {
            _logger.LogInformation("[AutomationTimer] {reqId} Local epoch will updated, {oldEpoch} => {newEpoch}",
                reqId,
                requestStartArgs.Epoch, request.Epoch);
            requestStartArgs.Epoch = request.Epoch;
            _epochDict[argId] = request.Epoch;
        }

        await _jobManager.EnqueueAsync(requestStartArgs);
    }
}