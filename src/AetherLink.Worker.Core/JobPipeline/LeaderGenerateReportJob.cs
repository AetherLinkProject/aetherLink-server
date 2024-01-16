using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Consts;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.PeerManager;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Scheduler;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.JobPipeline;

public class LeaderGenerateReportJob : AsyncBackgroundJob<LeaderGenerateReportJobArgs>, ISingletonDependency
{
    private readonly IPeerManager _peerManager;
    private readonly OracleInfoOptions _options;
    private readonly IObjectMapper _objectMapper;
    private readonly IStateProvider _stateProvider;
    private readonly ISchedulerService _schedulerService;
    private static readonly ReaderWriterLock Lock = new();
    private readonly IJobRequestProvider _jobRequestProvider;
    private readonly ILogger<LeaderGenerateReportJob> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public LeaderGenerateReportJob(IJobRequestProvider jobRequestProvider, ILogger<LeaderGenerateReportJob> logger,
        IPeerManager peerManager, IBackgroundJobManager backgroundJobManager, ISchedulerService schedulerService,
        IOptionsSnapshot<OracleInfoOptions> options, IStateProvider stateProvider, IObjectMapper objectMapper)
    {
        _logger = logger;
        _options = options.Value;
        _peerManager = peerManager;
        _objectMapper = objectMapper;
        _stateProvider = stateProvider;
        _schedulerService = schedulerService;
        _jobRequestProvider = jobRequestProvider;
        _backgroundJobManager = backgroundJobManager;
    }

    public override async Task ExecuteAsync(LeaderGenerateReportJobArgs args)
    {
        var reqId = args.RequestId;
        var chainId = args.ChainId;
        var roundId = args.RoundId;
        var index = args.Index;
        var epoch = args.Epoch;
        var argsName = IdGeneratorHelper.GenerateId(chainId, reqId, epoch, roundId);
        _logger.LogInformation("[Step3][Leader] {name} Start", argsName);

        try
        {
            // check epoch state
            var request = await _jobRequestProvider.GetJobRequestAsync(chainId, reqId, epoch);
            if (request == null || request.State == RequestState.RequestEnd) return;
            if (!await _peerManager.IsLeaderAsync(chainId, roundId)) return;

            var reportId = GenerateReportId(args);
            if (!IsEnough(args)) return;
            _logger.LogInformation("[Step3][Leader] {name} Generate report, index:{index}", argsName, index);

            var queue = _stateProvider.GetPartialObservation(reportId);
            var partialReportList = queue == null ? new List<ObservationDto>() : queue.ToArray().ToList();
            var observations = partialReportList.OrderBy(p => p.Index).Select(p => p.ObservationResult).ToList();

            await _jobRequestProvider.SetReportAsync(new ReportDto
            {
                ChainId = chainId,
                RequestId = reqId,
                RoundId = roundId,
                Epoch = epoch,
                Observations = observations
            });

            // save request.
            request.RoundId = roundId;
            request.ReportSendTime = DateTime.UtcNow;
            request.State = RequestState.ReportGenerated;
            await _jobRequestProvider.SetJobRequestAsync(request);

            _logger.LogInformation("[Step3][Leader] {name} Insert report process queue.", argsName);

            var reportStartSignTime = Timestamp.FromDateTime(DateTime.UtcNow);
            var procJob = _objectMapper.Map<LeaderGenerateReportJobArgs, FollowerReportProcessJobArgs>(args);
            procJob.ReportStartSignTime = reportStartSignTime;
            procJob.Observations = observations;
            await _backgroundJobManager.EnqueueAsync(procJob);

            _schedulerService.CancelScheduler(request, SchedulerType.CheckObservationResultCommitScheduler);

            await _peerManager.BroadcastRequestAsync(new StreamMessage
            {
                MessageType = MessageType.RequestReport,
                RequestId = reqId,
                Message = new Observations
                {
                    RequestId = reqId,
                    ChainId = chainId,
                    RoundId = roundId,
                    Epoch = epoch,
                    ObservationResults = { observations },
                    StartTime = reportStartSignTime
                }.ToBytesValue().Value
            });

            // report commit check scheduler
            _schedulerService.StartScheduler(request, SchedulerType.CheckReportCommitScheduler);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Step3][Leader] {name} Generate report failed.", argsName);
        }
    }

    private bool IsEnough(LeaderGenerateReportJobArgs args)
    {
        try
        {
            Lock.AcquireWriterLock(Timeout.Infinite);
            if (!_options.ChainConfig.TryGetValue(args.ChainId, out var chainConfig)) return false;
            var reportId = GenerateReportId(args);

            var observations = _stateProvider.GetPartialObservation(reportId);
            if (observations == null)
            {
                _logger.LogInformation("[step3][Leader] Init report.");
                observations = new List<ObservationDto>();
                _stateProvider.SetPartialObservation(reportId, observations);
            }

            _logger.LogDebug("[step3][Leader] Observations count {count}", observations.Count);

            if (observations.Any(observation => observation.Index == args.Index)) return false;
            observations.Add(new ObservationDto
            {
                Index = args.Index,
                ObservationResult = args.Data
            });
            _stateProvider.SetPartialObservation(reportId, observations);

            var flag = _stateProvider.GetReportGeneratedFlag(reportId);
            if (observations.Count >= chainConfig.ObservationsThreshold && !flag)
            {
                _logger.LogInformation("[step3][Leader] Report enough, queue length {len}", observations.Count);
                _stateProvider.SetReportGeneratedFlag(reportId);
                return true;
            }

            _logger.LogWarning("[Step3][Leader] ReportGenerated failed. reqId:{Req}, index:{index}", args.RequestId,
                args.Index);
            return false;
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "[Step3][Leader] NotEnough or generated. reqId:{Req}, index:{index}", args.RequestId,
                args.Index);
            return false;
        }
        finally
        {
            Lock.ReleaseWriterLock();
        }
    }

    private string GenerateReportId(LeaderGenerateReportJobArgs args)
    {
        return IdGeneratorHelper.GenerateId(args.ChainId, args.RequestId, args.Epoch);
    }
}