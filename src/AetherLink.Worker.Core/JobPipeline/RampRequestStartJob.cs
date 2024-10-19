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

namespace AetherLink.Worker.Core.JobPipeline;

public class RampRequestStartJob : AsyncBackgroundJob<RampRequestStartJobArgs>, ITransientDependency
{
    private readonly IPeerManager _peerManager;
    private readonly IObjectMapper _objectMapper;
    private readonly ISchedulerService _schedulerService;
    private readonly ILogger<RampRequestStartJob> _logger;
    private readonly IRampMessageProvider _rampMessageProvider;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public RampRequestStartJob(ILogger<RampRequestStartJob> logger, IPeerManager peerManager,
        IObjectMapper objectMapper, ISchedulerService schedulerService, IBackgroundJobManager backgroundJobManager,
        IRampMessageProvider rampMessageProvider)
    {
        _logger = logger;
        _peerManager = peerManager;
        _objectMapper = objectMapper;
        _schedulerService = schedulerService;
        _rampMessageProvider = rampMessageProvider;
        _backgroundJobManager = backgroundJobManager;
    }

    public override async Task ExecuteAsync(RampRequestStartJobArgs args)
    {
        try
        {
            // new request, new round request, resend request
            var rampMessageData = await _rampMessageProvider.GetAsync(args.MessageId);
            if (rampMessageData == null)
            {
                rampMessageData = _objectMapper.Map<RampRequestStartJobArgs, RampMessageDto>(args);
                var receivedTime = DateTimeOffset.FromUnixTimeMilliseconds(args.StartTime).DateTime;
                rampMessageData.RequestReceiveTime = receivedTime;
            }
            else if (args.RoundId > 0)
            {
                // reset new received time to next time window 
            }
            else switch (rampMessageData.State)
            {
                case RampRequestState.RequestStart:
                case RampRequestState.Committed:
                case RampRequestState.Confirmed:
                    _logger.LogDebug("Request already started.");
                    return;
                case RampRequestState.PendingResend:
                    // reset new received time to resend transaction time
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            rampMessageData.State = RampRequestState.RequestStart;
            await _rampMessageProvider.SetAsync(rampMessageData);

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
                    RoundId = args.RoundId,
                    Epoch = args.Epoch
                }));
            }

            _schedulerService.StartScheduler(rampMessageData, RampSchedulerType.CheckCommittedScheduler);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "");
            throw;
        }
    }
}