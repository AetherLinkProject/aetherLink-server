using System;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.PeerManager;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Scheduler;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.JobPipeline.CrossChain;

public class CrossChainRequestStartJob : AsyncBackgroundJob<CrossChainRequestStartArgs>, ITransientDependency
{
    private readonly IPeerManager _peerManager;
    private readonly IObjectMapper _objectMapper;
    private readonly ISchedulerService _schedulerService;
    private readonly ILogger<CrossChainRequestStartJob> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ICrossChainRequestProvider _crossChainRequestProvider;

    public CrossChainRequestStartJob(ILogger<CrossChainRequestStartJob> logger, IPeerManager peerManager,
        ISchedulerService schedulerService, IBackgroundJobManager backgroundJobManager,
        ICrossChainRequestProvider crossChainRequestProvider, IObjectMapper objectMapper)
    {
        _logger = logger;
        _peerManager = peerManager;
        _schedulerService = schedulerService;
        _backgroundJobManager = backgroundJobManager;
        _crossChainRequestProvider = crossChainRequestProvider;
        _objectMapper = objectMapper;
    }

    public override async Task ExecuteAsync(CrossChainRequestStartArgs args)
    {
        _logger.LogDebug("[CrossChain] CrossChainRequest Start Job ...");

        var reportContext = args.ReportContext;
        try
        {
            // new request, new round request, resend request
            var crossChainData = await _crossChainRequestProvider.GetAsync(args.ReportContext.MessageId);
            if (crossChainData == null)
            {
                _logger.LogDebug($"[CrossChain] Get new cross chain request {reportContext.MessageId}");
                crossChainData = _objectMapper.Map<CrossChainRequestStartArgs, CrossChainDataDto>(args);
                var receivedTime = DateTimeOffset.FromUnixTimeMilliseconds(args.StartTime).DateTime;
                crossChainData.RequestReceiveTime = receivedTime;
            }
            else if (crossChainData.State == CrossChainState.RequestCanceled)
            {
                _logger.LogWarning($"[CrossChain] Request {reportContext.MessageId} canceled");
                return;
            }

            crossChainData.State = CrossChainState.RequestStart;
            await _crossChainRequestProvider.SetAsync(crossChainData);

            if (_peerManager.IsLeader(reportContext.Epoch, reportContext.RoundId))
            {
                _logger.LogInformation("[CrossChain][Leader] {msgId}-{epoch}-{round} Is Leader.",
                    reportContext.MessageId, reportContext.Epoch, reportContext.RoundId);
                await _backgroundJobManager.EnqueueAsync(
                    new CrossChainPartialSignatureJobArgs { ReportContext = reportContext },
                    BackgroundJobPriority.High);

                await _peerManager.BroadcastAsync(p => p.QueryMessageSignatureAsync(
                    _objectMapper.Map<CrossChainRequestStartArgs, QueryMessageSignatureRequest>(args)));
            }

            _schedulerService.StartScheduler(crossChainData, CrossChainSchedulerType.CheckCommittedScheduler);
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[CrossChain] Start request {reportContext.MessageId} failed");
        }
    }
}