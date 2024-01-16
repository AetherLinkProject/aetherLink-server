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
    private readonly IPeerManager _peerManager;
    private readonly IObjectMapper _objectMapper;
    private readonly IJobRequestProvider _jobRequestProvider;
    private readonly ILogger<DataFeedsTimerProvider> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ConcurrentDictionary<string, long> _epochDict = new();

    public DataFeedsTimerProvider(IBackgroundJobManager backgroundJobManager, ILogger<DataFeedsTimerProvider> logger,
        IPeerManager peerManager, IObjectMapper objectMapper, IJobRequestProvider jobRequestProvider)
    {
        _logger = logger;
        _peerManager = peerManager;
        _objectMapper = objectMapper;
        _jobRequestProvider = jobRequestProvider;
        _backgroundJobManager = backgroundJobManager;
    }

    public async Task ExecuteAsync(DataFeedsProcessJobArgs args)
    {
        var reqId = args.RequestId;
        var chainId = args.ChainId;
        var argsName = IdGeneratorHelper.GenerateId(chainId, reqId);
        _logger.LogInformation("[DataFeedsTimer] {name} Execute checking.", argsName);

        var currentEpoch = await _peerManager.GetEpochAsync(chainId);
        _epochDict.TryGetValue(chainId, out var epoch);
        if (currentEpoch == epoch &&
            _epochDict.TryGetValue(IdGeneratorHelper.GenerateId(chainId, reqId), out _))
        {
            _logger.LogInformation(
                "[DataFeedsTimer] The last epoch {Epoch} wasn't finished. reqId {reqId}, chainId : {chainId}",
                epoch, reqId, chainId);
            return;
        }

        _logger.LogInformation("[DataFeedsTimer] {ChainId} Local epoch will updated, {oldEpoch} => {newEpoch}",
            chainId, epoch, currentEpoch);
        _epochDict[IdGeneratorHelper.GenerateId(chainId, reqId)] = currentEpoch;
        _epochDict[chainId] = currentEpoch;

        // epoch != 0, request == null, request canceled & transmitted with new epoch
        // epoch != 0, request.Retrying = true, retry 
        // epoch == 0, request != null, node restart 
        var request = await _jobRequestProvider.GetJobRequestAsync(chainId, reqId, currentEpoch);
        if (epoch != 0 && request is { Retrying: true })
        {
            _logger.LogWarning("[DataFeedsTimer] {reqId}-{chainId}-{epoch} Request not exist.", reqId, chainId,
                currentEpoch);
            return;
        }

        _logger.LogInformation("[DataFeedsTimer] New {ChainId} epoch {Epoch} start.", chainId, currentEpoch);

        var requestArgs = _objectMapper.Map<DataFeedsProcessJobArgs, RequestStartProcessJobArgs>(args);
        requestArgs.Epoch = currentEpoch;

        await _backgroundJobManager.EnqueueAsync(requestArgs);
    }
}