using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AetherLink.Worker.Core.Common;
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

public class RequestStartProcessJob : AsyncBackgroundJob<RequestStartProcessJobArgs>, ITransientDependency
{
    private readonly IPeerManager _peerManager;
    private readonly IJobProvider _jobProvider;
    private readonly IObjectMapper _objectMapper;
    private readonly ISchedulerService _schedulerService;
    private readonly ILogger<RequestStartProcessJob> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public RequestStartProcessJob(IPeerManager peerManager, ILogger<RequestStartProcessJob> logger,
        IBackgroundJobManager backgroundJobManager, IObjectMapper objectMapper, ISchedulerService schedulerService,
        IJobProvider jobProvider)
    {
        _logger = logger;
        _peerManager = peerManager;
        _jobProvider = jobProvider;
        _objectMapper = objectMapper;
        _schedulerService = schedulerService;
        _backgroundJobManager = backgroundJobManager;
    }

    public override async Task ExecuteAsync(RequestStartProcessJobArgs args)
    {
        await Handler(args);
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(RequestCancelProcessJob),
        MethodName = nameof(HandleException))]
    public virtual async Task Handler(RequestStartProcessJobArgs args)
    {
        var argId = IdGeneratorHelper.GenerateId(args.ChainId, args.RequestId, args.Epoch, args.RoundId);
        var blockTime = DateTimeOffset.FromUnixTimeMilliseconds(args.StartTime).DateTime;

        var job = await _jobProvider.GetAsync(args);
        if (job == null)
        {
            job = _objectMapper.Map<RequestStartProcessJobArgs, JobDto>(args);
        }
        else if (job.State == RequestState.RequestCanceled || args.Epoch < job.Epoch)
        {
            return;
        }
        // Update blockTime through transmitted event time, used for node consensus task start time.
        else if (job.RoundId == 0 && job.State == RequestState.RequestEnd)
        {
            blockTime = DateTimeOffset.FromUnixTimeMilliseconds(job.TransactionBlockTime).DateTime;
        }

        // This transaction completion time is not the start time of scheduled task execution, so the node needs to align this time to the official execution time.
        job.RequestReceiveTime = _schedulerService.UpdateBlockTime(blockTime);
        job.RoundId = args.RoundId;
        job.State = RequestState.RequestStart;
        await _jobProvider.SetAsync(job);

        _logger.LogDebug("[Step1] {name} start startTime {time}, blockTime {blockTime}", argId, args.StartTime,
            job.TransactionBlockTime);

        if (_peerManager.IsLeader(args.Epoch, args.RoundId))
        {
            _logger.LogInformation("[Step1][Leader] {name} Is Leader.", argId);
            await _backgroundJobManager.EnqueueAsync(
                _objectMapper.Map<RequestStartProcessJobArgs, CollectObservationJobArgs>(args),
                BackgroundJobPriority.High);

            await _peerManager.BroadcastAsync(p => p.QueryObservationAsync(new QueryObservationRequest
            {
                RequestId = args.RequestId,
                ChainId = args.ChainId,
                RoundId = args.RoundId,
                Epoch = args.Epoch
            }));
        }

        _schedulerService.StartScheduler(job, SchedulerType.CheckRequestEndScheduler);

        _logger.LogInformation("[step1] {name} Waiting for request end.", argId);
    }

    #region Exception Handing

    public async Task<FlowBehavior> HandleException(Exception ex, RequestCancelProcessJobArgs args)
    {
        var argId = IdGeneratorHelper.GenerateId(args.ChainId, args.RequestId, args.Epoch, args.RoundId);
        _logger.LogError(ex, "[step1] {name} RequestStart process failed.", argId);

        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return
        };
    }

    #endregion
}