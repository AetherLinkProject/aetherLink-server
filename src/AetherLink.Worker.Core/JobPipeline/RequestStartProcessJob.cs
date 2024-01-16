using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using AElf;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Consts;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.PeerManager;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Scheduler;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.JobPipeline;

public class RequestStartProcessJob : AsyncBackgroundJob<RequestStartProcessJobArgs>, ISingletonDependency
{
    private readonly IPeerManager _peerManager;
    private readonly IObjectMapper _objectMapper;
    private readonly ISchedulerService _schedulerService;
    private readonly ILogger<RequestStartProcessJob> _logger;
    private readonly IJobRequestProvider _jobRequestProvider;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ConcurrentDictionary<string, long> _currentEpochs = new();

    public RequestStartProcessJob(IPeerManager peerManager, ILogger<RequestStartProcessJob> logger,
        IJobRequestProvider jobRequestProvider, IBackgroundJobManager backgroundJobManager,
        ISchedulerService schedulerService, IObjectMapper objectMapper)
    {
        _logger = logger;
        _peerManager = peerManager;
        _objectMapper = objectMapper;
        _schedulerService = schedulerService;
        _jobRequestProvider = jobRequestProvider;
        _backgroundJobManager = backgroundJobManager;
    }

    public override async Task ExecuteAsync(RequestStartProcessJobArgs args)
    {
        var reqId = args.RequestId;
        var roundId = args.RoundId;
        var epoch = args.Epoch;
        var chainId = args.ChainId;
        var argsName = IdGeneratorHelper.GenerateId(chainId, reqId, epoch, roundId);

        var currentEpochId = IdGeneratorHelper.GenerateId(chainId, reqId);
        if (_currentEpochs.TryGetValue(currentEpochId, out var currentEpoch) && epoch < currentEpoch)
        {
            _logger.LogInformation("[Step1] The epoch in the request {name} is older than the local {epoch}",
                argsName, currentEpoch);
            return;
        }

        _logger.LogInformation("[Step1] ============================ {name} start =========================", argsName);
        var blockTime = DateTimeOffset.FromUnixTimeMilliseconds(args.StartTime).DateTime;

        var request = await _jobRequestProvider.GetJobRequestAsync(chainId, reqId, epoch);
        if (request == null)
        {
            request = _objectMapper.Map<RequestStartProcessJobArgs, RequestDto>(args);
        }
        // for node restart and transmitted update request
        else if (request.RoundId == 0)
        {
            // get block time from request because args is old transaction time which is over time yet.
            blockTime = DateTimeOffset.FromUnixTimeMilliseconds(request.TransactionBlockTime).DateTime;
            request.JobSpec = args.JobSpec;
        }

        _logger.LogDebug("[Step1] startTime {time}, blockTime {blockTime}", args.StartTime,
            request.TransactionBlockTime);

        request.RequestReceiveTime = _schedulerService.UpdateBlockTime(blockTime);
        var utcNow = DateTime.UtcNow;
        request.RoundId = roundId;

        if (await _peerManager.IsLeaderAsync(chainId, roundId))
        {
            _logger.LogInformation("[Step1][Leader] {name} Is Leader.", argsName);

            // record observation request start time
            request.RequestStartTime = utcNow;
            request.State = RequestState.RequestStart;
            await _jobRequestProvider.SetJobRequestAsync(request);

            var procJob = _objectMapper.Map<RequestStartProcessJobArgs, FollowerObservationProcessJobArgs>(args);
            procJob.RequestStartTime = utcNow.ToTimestamp();
            await _backgroundJobManager.EnqueueAsync(procJob, BackgroundJobPriority.High);

            //broadcast request
            await _peerManager.BroadcastRequestAsync(new StreamMessage
            {
                MessageType = MessageType.RequestJob,
                RequestId = reqId,
                Message = new RequestJob
                {
                    RequestId = reqId,
                    ChainId = chainId,
                    RoundId = roundId,
                    StartTime = Timestamp.FromDateTime(utcNow),
                    Epoch = epoch
                }.ToBytesValue().Value
            });

            // data commit check scheduler
            _schedulerService.StartScheduler(request, SchedulerType.CheckObservationResultCommitScheduler);
        }
        else
        {
            // record follower log receive time
            request.State = RequestState.RequestPending;
            await _jobRequestProvider.SetJobRequestAsync(request);

            _schedulerService.StartScheduler(request, SchedulerType.CheckRequestReceiveScheduler);
        }

        _logger.LogInformation("[step1] {name} Waiting for request end.", argsName);
        _schedulerService.StartScheduler(request, SchedulerType.CheckRequestEndScheduler);
        _currentEpochs[currentEpochId] = args.Epoch;
    }
}