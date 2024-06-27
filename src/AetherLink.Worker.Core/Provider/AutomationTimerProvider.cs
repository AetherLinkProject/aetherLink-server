using System.Collections.Concurrent;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Automation.Args;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.JobPipeline.Args;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.Provider;

public class AutomationTimerProvider : ISingletonDependency
{
    private readonly IJobProvider _jobProvider;
    private readonly IObjectMapper _objectMapper;
    private readonly IBackgroundJobManager _jobManager;
    private readonly ILogger<AutomationTimerProvider> _logger;
    private readonly ConcurrentDictionary<string, long> _epochDict = new();

    public AutomationTimerProvider(IBackgroundJobManager jobManager, ILogger<AutomationTimerProvider> logger,
        IObjectMapper objectMapper, IJobProvider jobProvider)
    {
        _logger = logger;
        _jobManager = jobManager;
        _jobProvider = jobProvider;
        _objectMapper = objectMapper;
    }

    public async Task ExecuteAsync(AutomationJobArgs args)
    {
        var reqId = args.RequestId;
        var chainId = args.ChainId;
        var argId = IdGeneratorHelper.GenerateId(chainId, reqId);
        _logger.LogInformation("[DataFeedsTimer] {name} Execute checking.", argId);

        var request = await _jobProvider.GetAsync(args);
        if (request == null)
        {
            // only new request will update epochDict from args Epoch, others will updated epoch by request epoch from transmitted logevent
            _epochDict[argId] = args.Epoch;
            await _jobManager.EnqueueAsync(_objectMapper.Map<AutomationJobArgs, RequestStartProcessJobArgs>(args));
            return;
        }

        // this epoch not finished, Wait for transmitted log event.
        _epochDict.TryGetValue(argId, out var epoch);
        if (request.Epoch == epoch && request.Epoch != 0)
        {
            _logger.LogInformation("[AutomationTimer] The last epoch {Epoch} wasn't finished. reqId {reqId}", epoch,
                reqId);
            return;
        }

        // when node restart _epochDict request is existed, and epoch is 0, 0 => newEpoch
        _logger.LogInformation("[AutomationTimer] {reqId} Local epoch will updated, {oldEpoch} => {newEpoch}", reqId,
            epoch, request.Epoch);

        var requestStartArgs = _objectMapper.Map<AutomationJobArgs, RequestStartProcessJobArgs>(args);
        requestStartArgs.Epoch = request.Epoch;
        await _jobManager.EnqueueAsync(requestStartArgs);

        _epochDict[argId] = request.Epoch;
    }
}