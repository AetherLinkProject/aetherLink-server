using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.PeerManager;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Scheduler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.JobPipeline;

public class GenerateReportJob : AsyncBackgroundJob<GenerateReportJobArgs>, ISingletonDependency
{
    private readonly object _lock;
    private readonly IPeerManager _peerManager;
    private readonly IJobProvider _jobProvider;
    private readonly OracleInfoOptions _options;
    private readonly IObjectMapper _objectMapper;
    private readonly IStateProvider _stateProvider;
    private readonly IReportProvider _reportProvider;
    private readonly ILogger<GenerateReportJob> _logger;
    private readonly ISchedulerService _schedulerService;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public GenerateReportJob(ILogger<GenerateReportJob> logger, ISchedulerService schedulerService,
        IObjectMapper objectMapper, IStateProvider stateProvider, IOptionsSnapshot<OracleInfoOptions> options,
        IJobProvider jobProvider, IReportProvider reportProvider, IPeerManager peerManager,
        IBackgroundJobManager backgroundJobManager)
    {
        _logger = logger;
        _lock = new object();
        _options = options.Value;
        _peerManager = peerManager;
        _jobProvider = jobProvider;
        _objectMapper = objectMapper;
        _stateProvider = stateProvider;
        _reportProvider = reportProvider;
        _schedulerService = schedulerService;
        _backgroundJobManager = backgroundJobManager;
    }

    public override async Task ExecuteAsync(GenerateReportJobArgs args)
    {
        var reqId = args.RequestId;
        var chainId = args.ChainId;
        var roundId = args.RoundId;
        var index = args.Index;
        var epoch = args.Epoch;
        var argId = IdGeneratorHelper.GenerateId(chainId, reqId, epoch, roundId);

        try
        {
            // check epoch state
            var job = await _jobProvider.GetAsync(args);
            if (job == null || job.State is RequestState.RequestCanceled) return;

            var reportId = IdGeneratorHelper.GenerateReportId(args);
            if (_stateProvider.IsFinished(reportId)) return;

            _logger.LogInformation("[Step3][Leader] {name} Start process {index} request.", argId, index);

            TryProcessObservationAsync(args, job);

            if (!IsReportEnough(args))
            {
                _logger.LogDebug("[Step3][Leader] {name} is not enough, no need to generate report.", argId);
                return;
            }

            if (_stateProvider.IsFinished(reportId))
            {
                _logger.LogDebug("[Step3][Leader] {name} report is finished.", argId);
                return;
            }

            _stateProvider.SetFinishedFlag(reportId);

            _logger.LogInformation("[Step3][Leader] {name} Generate report, index:{index}", argId, index);

            var observations = GenerateObservations(args);
            await _reportProvider.SetAsync(new ReportDto
            {
                ChainId = chainId,
                RequestId = reqId,
                RoundId = roundId,
                Epoch = epoch,
                Observations = observations
            });

            await ProcessReportGeneratedResultAsync(job, observations);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Step3][Leader] {name} Generate report failed.", argId);
        }
    }

    private bool IsReportEnough(GenerateReportJobArgs args)
    {
        if (!_options.ChainConfig.TryGetValue(args.ChainId, out var chainConfig)) return false;

        var observations = _stateProvider.GetObservations(IdGeneratorHelper.GenerateReportId(args));
        if (observations == null) return false;

        return observations.Count >= chainConfig.ObservationsThreshold;
    }

    private void TryProcessObservationAsync(GenerateReportJobArgs args, JobDto job)
    {
        lock (_lock)
        {
            var observation = new ObservationDto
            {
                Index = args.Index,
                ObservationResult = args.Data
            };

            var reportId = IdGeneratorHelper.GenerateReportId(args);
            var observations = _stateProvider.GetObservations(reportId);
            if (observations == null)
            {
                _logger.LogInformation("[step3][Leader] Init {id}, start observation collection scheduler",
                    reportId);
                _stateProvider.SetObservations(reportId, new List<ObservationDto> { observation });
                _schedulerService.StartScheduler(job, SchedulerType.ObservationCollectWaitingScheduler);
                return;
            }

            if (observations.Any(o => o.Index == observation.Index)) return;
            observations.Add(observation);
            _stateProvider.SetObservations(reportId, observations);
        }
    }

    private List<long> GenerateObservations(GenerateReportJobArgs args)
    {
        var aggregationResults = Enumerable.Repeat(0L, _peerManager.GetPeersCount()).ToList();
        _stateProvider.GetObservations(IdGeneratorHelper.GenerateReportId(args))
            .ForEach(o => aggregationResults[o.Index] = o.ObservationResult);
        _logger.LogDebug("[Step3][Leader] {requestId} report:{results} count:{count}", args.RequestId,
            aggregationResults.JoinAsString(","), aggregationResults.Count);
        return aggregationResults;
    }

    private async Task ProcessReportGeneratedResultAsync(JobDto job, List<long> observations)
    {
        // cancel ObservationCollectWaiting scheduler if observations collection is enough
        _schedulerService.CancelScheduler(job, SchedulerType.ObservationCollectWaitingScheduler);

        var procJob = _objectMapper.Map<JobDto, GeneratePartialSignatureJobArgs>(job);
        procJob.Observations = observations;
        await _backgroundJobManager.EnqueueAsync(procJob);

        await _peerManager.BroadcastAsync(p => p.CommitReportAsync(new CommitReportRequest
        {
            RequestId = job.RequestId,
            ChainId = job.ChainId,
            RoundId = job.RoundId,
            Epoch = job.Epoch,
            ObservationResults = { observations }
        }));
    }
}