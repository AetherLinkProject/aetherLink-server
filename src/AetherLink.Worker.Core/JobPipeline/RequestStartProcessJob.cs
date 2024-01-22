using System;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Consts;
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

public class RequestStartProcessJob : AsyncBackgroundJob<RequestStartProcessJobArgs>, ITransientDependency
{
    private readonly IPeerManager _peerManager;
    private readonly IObjectMapper _objectMapper;
    private readonly IRequestProvider _requestProvider;
    private readonly ISchedulerService _schedulerService;
    private readonly ILogger<RequestStartProcessJob> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public RequestStartProcessJob(IPeerManager peerManager, ILogger<RequestStartProcessJob> logger,
        IBackgroundJobManager backgroundJobManager, IObjectMapper objectMapper, ISchedulerService schedulerService,
        IRequestProvider requestProvider)
    {
        _logger = logger;
        _peerManager = peerManager;
        _objectMapper = objectMapper;
        _requestProvider = requestProvider;
        _schedulerService = schedulerService;
        _backgroundJobManager = backgroundJobManager;
    }

    public override async Task ExecuteAsync(RequestStartProcessJobArgs args)
    {
        var argId = IdGeneratorHelper.GenerateId(args.ChainId, args.RequestId, args.Epoch, args.RoundId);
        var blockTime = DateTimeOffset.FromUnixTimeMilliseconds(args.StartTime).DateTime;

        try
        {
            var request = await _requestProvider.GetAsync(args);
            if (request == null)
            {
                request = _objectMapper.Map<RequestStartProcessJobArgs, RequestDto>(args);
            }
            else if (request.State == RequestState.RequestCanceled || args.Epoch < request.Epoch)
            {
                return;
            }
            // Update blockTime through transmitted event time, used for node consensus task start time.
            else if (request.RoundId == 0 && request.State == RequestState.RequestEnd)
            {
                blockTime = DateTimeOffset.FromUnixTimeMilliseconds(request.TransactionBlockTime).DateTime;
            }

            // This transaction completion time is not the start time of scheduled task execution, so the node needs to align this time to the official execution time.
            request.RequestReceiveTime = _schedulerService.UpdateBlockTime(blockTime);
            request.RoundId = args.RoundId;
            request.State = RequestState.RequestStart;
            await _requestProvider.SetAsync(request);

            _logger.LogDebug("[Step1] {name} start startTime {time}, blockTime {blockTime}", argId, args.StartTime,
                request.TransactionBlockTime);

            if (_peerManager.IsLeader(args.Epoch, args.RoundId))
            {
                _logger.LogInformation("[Step1][Leader] {name} Is Leader.", argId);
                await _backgroundJobManager.EnqueueAsync(
                    _objectMapper.Map<RequestStartProcessJobArgs, CollectObservationJobArgs>(args),
                    BackgroundJobPriority.High);

                await _peerManager.BroadcastAsync(p => p.QueryObservationAsync(
                    new QueryObservationRequest
                    {
                        RequestId = args.RequestId,
                        ChainId = args.ChainId,
                        RoundId = args.RoundId,
                        Epoch = args.Epoch
                    }));
            }

            _schedulerService.StartScheduler(request, SchedulerType.CheckRequestEndScheduler);

            _logger.LogInformation("[step1] {name} Waiting for request end.", argId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[step1] {name} RequestStart process failed.", argId);
        }
    }
}