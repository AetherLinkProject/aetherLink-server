using System.Collections.Concurrent;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.JobPipeline.Args;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.Provider;

public class DataFeedsTimerProvider : ISingletonDependency
{
    private readonly IObjectMapper _objectMapper;
    private readonly IRequestProvider _requestProvider;
    private readonly ILogger<DataFeedsTimerProvider> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ConcurrentDictionary<string, long> _epochDict = new();

    public DataFeedsTimerProvider(IBackgroundJobManager backgroundJobManager, ILogger<DataFeedsTimerProvider> logger,
        IObjectMapper objectMapper, IRequestProvider requestProvider)
    {
        _logger = logger;
        _objectMapper = objectMapper;
        _requestProvider = requestProvider;
        _backgroundJobManager = backgroundJobManager;
    }

    public async Task ExecuteAsync(DataFeedsProcessJobArgs args)
    {
        var reqId = args.RequestId;
        var chainId = args.ChainId;
        var argId = IdGeneratorHelper.GenerateId(chainId, reqId);
        _logger.LogInformation("[DataFeedsTimer] {name} Execute checking.", argId);

        var request = await _requestProvider.GetAsync(args);
        if (request == null)
        {
            // only new request will update epochDict from args Epoch, others will updated epoch by request epoch from transmitted logevent
            _epochDict[argId] = args.Epoch;
            await _backgroundJobManager.EnqueueAsync(
                _objectMapper.Map<DataFeedsProcessJobArgs, RequestStartProcessJobArgs>(args));
            return;
        }

        // this epoch not finished, Wait for transmitted log event.
        _epochDict.TryGetValue(argId, out var epoch);
        if (request.Epoch == epoch && request.Epoch != 0)
        {
            _logger.LogInformation("[DataFeedsTimer] The last epoch {Epoch} wasn't finished. reqId {reqId}", epoch,
                reqId);
            return;
        }

        // when node restart _epochDict request is existed, and epoch is 0, 0 => newEpoch
        _logger.LogInformation("[DataFeedsTimer] {reqId} Local epoch will updated, {oldEpoch} => {newEpoch}", reqId,
            epoch, request.Epoch);

        var requestStartArgs = _objectMapper.Map<DataFeedsProcessJobArgs, RequestStartProcessJobArgs>(args);
        requestStartArgs.Epoch = request.Epoch;
        await _backgroundJobManager.EnqueueAsync(requestStartArgs);

        _epochDict[argId] = request.Epoch;
    }
}