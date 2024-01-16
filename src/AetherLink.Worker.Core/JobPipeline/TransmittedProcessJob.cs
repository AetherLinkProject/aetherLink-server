using System;
using System.Collections.Concurrent;
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

namespace AetherLink.Worker.Core.JobPipeline;

public class TransmittedProcessJob : AsyncBackgroundJob<TransmittedProcessJobArgs>, ISingletonDependency
{
    private readonly IPeerManager _peerManager;
    private readonly IIndexerProvider _indexerProvider;
    private readonly ISchedulerService _schedulerService;
    private readonly ILogger<TransmittedProcessJob> _logger;
    private readonly IJobRequestProvider _jobRequestProvider;
    private readonly ConcurrentDictionary<string, long> _epochDict;

    public TransmittedProcessJob(IPeerManager peerManager, ISchedulerService schedulerService,
        IJobRequestProvider jobRequestProvider, ILogger<TransmittedProcessJob> logger, IIndexerProvider indexerProvider)
    {
        _peerManager = peerManager;
        _schedulerService = schedulerService;
        _jobRequestProvider = jobRequestProvider;
        _logger = logger;
        _indexerProvider = indexerProvider;
        _epochDict = new ConcurrentDictionary<string, long>();
    }

    public override async Task ExecuteAsync(TransmittedProcessJobArgs args)
    {
        var reqId = args.RequestId;
        var chainId = args.ChainId;
        var newEpoch = args.Epoch;
        var argsName = IdGeneratorHelper.GenerateId(chainId, reqId, newEpoch);

        var epoch = await _peerManager.GetEpochAsync(chainId);
        if (epoch != 0 && newEpoch != 0 && epoch == newEpoch)
        {
            _logger.LogWarning("[Transmitted] {name} epoch:{epoch} equal newEpoch:{newEpoch}", argsName, epoch,
                newEpoch);
            return;
        }

        var chainEpochExist = _epochDict.TryGetValue(chainId, out _);
        var beforeEpoch = epoch;
        if (!chainEpochExist && epoch != 0) beforeEpoch -= 1;

        _logger.LogInformation("[Transmitted] Get transmitted event, {name} epoch:{epoch} newEpoch:{newEpoch}",
            argsName, epoch, newEpoch);

        var ocrJobEvents = await _indexerProvider.GetJobsAsync(chainId, reqId, RequestTypeConst.Vrf);
        if (ocrJobEvents.Count != 0 && chainEpochExist)
        {
            _logger.LogInformation("[Transmitted] VRF transmitted, no need update epoch. reqId:{ReqId}", reqId);
            return;
        }

        // Cancel before epoch Scheduler
        for (long i = beforeEpoch; i < newEpoch; i++)
        {
            var requestName = IdGeneratorHelper.GenerateId(chainId, reqId, i);
            var request = await _jobRequestProvider.GetJobRequestAsync(chainId, reqId, i);
            if (request == null)
            {
                _logger.LogWarning("[Transmitted] {name} Request not exist", requestName);
                var nowTime = DateTime.Now;
                request = new RequestDto
                {
                    Epoch = i,
                    RoundId = 0,
                    RequestId = reqId,
                    ChainId = chainId,
                    ReportSendTime = nowTime,
                    ReportSignTime = nowTime,
                    RequestStartTime = nowTime,
                    RequestReceiveTime = nowTime,
                    ObservationResultCommitTime = nowTime
                };
            }

            request.State = RequestState.RequestEnd;
            request.RequestEndTime = DateTime.UtcNow;
            await _jobRequestProvider.SetJobRequestAsync(request);

            _schedulerService.CancelScheduler(request, SchedulerType.CheckTransmitScheduler);
            _schedulerService.CancelScheduler(request, SchedulerType.CheckRequestEndScheduler);
        }

        _logger.LogInformation("======================{name} end =========================", argsName);

        var requestDto = new RequestDto
        {
            TransactionBlockTime = args.StartTime,
            ChainId = chainId,
            RequestId = reqId,
            Epoch = newEpoch,
            RoundId = 0
        };
        await _jobRequestProvider.SetJobRequestAsync(requestDto);
        _logger.LogInformation("[Transmitted] {name} Update block-time to {time}", argsName, args.StartTime);

        // update peer manager epoch before update job request, because cronjob will search job by epoch before executing.
        _peerManager.UpdateEpoch(chainId, newEpoch);
        _epochDict[chainId] = newEpoch;
    }
}