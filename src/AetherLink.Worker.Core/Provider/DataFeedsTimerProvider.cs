using System.Collections.Concurrent;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.PeerManager;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.Provider;

public class DataFeedsTimerProvider : ISingletonDependency
{
    private readonly IJobProvider _jobProvider;
    private readonly IPeerManager _peerManager;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<DataFeedsTimerProvider> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ConcurrentDictionary<string, long> _epochDict = new();

    public DataFeedsTimerProvider(IBackgroundJobManager backgroundJobManager, ILogger<DataFeedsTimerProvider> logger,
        IObjectMapper objectMapper, IJobProvider jobProvider, IPeerManager peerManager)
    {
        _logger = logger;
        _jobProvider = jobProvider;
        _peerManager = peerManager;
        _objectMapper = objectMapper;
        _backgroundJobManager = backgroundJobManager;
    }

    // The timer checks whether round needs to be updated through cron. If it is updated, a new task is started.
    public async Task ExecuteAsync(DataFeedsProcessJobArgs args)
    {
        var reqId = args.RequestId;
        var chainId = args.ChainId;
        var argId = IdGeneratorHelper.GenerateId(chainId, reqId);
        _logger.LogInformation("[DataFeedsTimer] {name} Execute checking.", argId);

        var requestStartArgs = _objectMapper.Map<DataFeedsProcessJobArgs, RequestStartProcessJobArgs>(args);
        var request = await _jobProvider.GetAsync(args);
        if (request == null)
        {
            // only new request will update epochDict from args Epoch, others will updated epoch by request epoch from transmitted logevent
            _epochDict[argId] = args.Epoch;
            await _backgroundJobManager.EnqueueAsync(requestStartArgs);
            return;
        }

        // this epoch not finished, Wait for transmitted log event.
        _epochDict.TryGetValue(argId, out var epoch);
        if (request.Epoch == epoch && request.Epoch != 0)
        {
            var newRoundId =
                _peerManager.GetCurrentRoundId(request.RequestReceiveTime, request.RequestEndTimeoutWindow);
            if (newRoundId <= request.RoundId)
            {
                _logger.LogDebug("[DataFeedsTimer] The last round {Epoch} wasn't finished. reqId {reqId}",
                    request.RoundId, reqId);
                return;
            }

            _logger.LogInformation("[DataFeedsTimer] {reqId} New round will start, {or} => {nr}", reqId,
                request.RoundId, newRoundId);
            requestStartArgs.RoundId = newRoundId;
        }
        else
        {
            _logger.LogInformation("[DataFeedsTimer] {reqId} New epoch will start, {oldEpoch} => {newEpoch}", reqId,
                requestStartArgs.Epoch, request.Epoch);
            _epochDict[argId] = request.Epoch;
        }

        requestStartArgs.Epoch = request.Epoch;
        await _backgroundJobManager.EnqueueAsync(requestStartArgs);
    }
}