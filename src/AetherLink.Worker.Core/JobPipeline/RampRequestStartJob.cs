using System;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.PeerManager;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Scheduler;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.JobPipeline;

public class RampRequestStartJob : AsyncBackgroundJob<RampRequestStartJobArgs>, ITransientDependency
{
    private readonly IPeerManager _peerManager;
    private readonly IObjectMapper _objectMapper;
    private readonly ISchedulerService _schedulerService;
    private readonly ILogger<RampRequestStartJob> _logger;
    private readonly IRampMessageProvider _requestProvider;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public RampRequestStartJob(ILogger<RampRequestStartJob> logger, IPeerManager peerManager,
        IObjectMapper objectMapper, ISchedulerService schedulerService, IBackgroundJobManager backgroundJobManager,
        IRampMessageProvider requestProvider)
    {
        _logger = logger;
        _peerManager = peerManager;
        _objectMapper = objectMapper;
        _requestProvider = requestProvider;
        _schedulerService = schedulerService;
        _backgroundJobManager = backgroundJobManager;
    }

    public override async Task ExecuteAsync(RampRequestStartJobArgs args)
    {
        try
        {
            var rampMessageData = _objectMapper.Map<RampRequestStartJobArgs, RampMessageDto>(args);
            var receivedTime = DateTimeOffset.FromUnixTimeMilliseconds(args.StartTime).DateTime;
            rampMessageData.RequestReceiveTime = receivedTime;
            rampMessageData.State = RampRequestState.RequestStart;
            await _requestProvider.SetAsync(rampMessageData);

            if (_peerManager.IsLeader(args.Epoch, args.RoundId))
            {
                _logger.LogInformation("[Ramp][Leader] {msgId}-{epoch}-{round} Is Leader.", args.MessageId, args.Epoch,
                    args.RoundId);
                await _backgroundJobManager.EnqueueAsync(
                    _objectMapper.Map<RampRequestStartJobArgs, RampRequestPartialSignatureJobArgs>(args),
                    BackgroundJobPriority.High);

                await _peerManager.BroadcastAsync(p => p.QueryMessageSignatureAsync(new QueryMessageSignatureRequest
                {
                    MessageId = args.MessageId,
                    ChainId = args.ChainId,
                    RoundId = args.RoundId,
                    Epoch = args.Epoch
                }));
            }

            _schedulerService.StartScheduler(rampMessageData);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "");
            throw;
        }
    }
}