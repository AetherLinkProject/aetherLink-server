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
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.Scheduler;

public interface IObservationCollectSchedulerJob
{
    Task Execute(JobDto job);
}

public class ObservationCollectSchedulerJob : IObservationCollectSchedulerJob, ITransientDependency
{
    private readonly IJobProvider _jobProvider;
    private readonly IPeerManager _peerManager;
    private readonly OracleInfoOptions _options;
    private readonly IObjectMapper _objectMapper;
    private readonly IStateProvider _stateProvider;
    private readonly IReportProvider _reportProvider;
    private readonly ILogger<ResetRequestSchedulerJob> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public ObservationCollectSchedulerJob(IObjectMapper objectMapper, ILogger<ResetRequestSchedulerJob> logger,
        IOptionsSnapshot<OracleInfoOptions> options, IStateProvider stateProvider, IJobProvider jobProvider,
        IReportProvider reportProvider, IPeerManager peerManager, IBackgroundJobManager backgroundJobManager)
    {
        _logger = logger;
        _options = options.Value;
        _jobProvider = jobProvider;
        _peerManager = peerManager;
        _objectMapper = objectMapper;
        _stateProvider = stateProvider;
        _reportProvider = reportProvider;
        _backgroundJobManager = backgroundJobManager;
    }

    public async Task Execute(JobDto job)
    {
        try
        {
            var chainId = job.ChainId;
            var reqId = job.RequestId;
            var roundId = job.RoundId;
            var epoch = job.Epoch;

            _logger.LogInformation(
                "[ObservationCollectScheduler] Scheduler job execute. reqId {ReqId}, roundId:{RoundId}, reqState:{State}",
                job.RequestId, job.RoundId, job.State.ToString());

            if (!_options.ChainConfig.TryGetValue(job.ChainId, out var chainConfig)) return;
            var reportId = IdGeneratorHelper.GenerateReportId(job: job);

            var observations = _stateProvider.GetObservations(reportId);
            if (observations == null || observations.Count < chainConfig.PartialSignaturesThreshold)
            {
                _logger.LogInformation("[ObservationCollectScheduler] Observation collection not enough.");
                return;
            }

            var collectResult = Enumerable.Repeat(0L, _peerManager.GetPeersCount()).ToList();
            _stateProvider.GetObservations(reportId).ForEach(o => collectResult[o.Index] = o.ObservationResult);
            _logger.LogDebug("[Step3][Leader] {requestId} report:{results} count:{count}", job.RequestId,
                collectResult.JoinAsString(","), collectResult.Count);

            await _reportProvider.SetAsync(new ReportDto
            {
                ChainId = chainId,
                RequestId = reqId,
                RoundId = roundId,
                Epoch = epoch,
                Observations = collectResult
            });

            // save job.
            job.RoundId = roundId;
            await _jobProvider.SetAsync(job);

            var reportStartSignTime = Timestamp.FromDateTime(DateTime.UtcNow);
            var args = _objectMapper.Map<JobDto, GeneratePartialSignatureJobArgs>(job);
            args.Observations = collectResult;

            await _backgroundJobManager.EnqueueAsync(args);

            await _peerManager.BroadcastAsync(p => p.CommitReportAsync(new CommitReportRequest
            {
                RequestId = job.RequestId,
                ChainId = job.ChainId,
                RoundId = job.RoundId,
                Epoch = job.Epoch,
                ObservationResults = { collectResult },
                StartTime = reportStartSignTime
            }));

            _stateProvider.SetFinishedFlag(reportId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[ObservationCollectScheduler] Observation collect scheduler execute failed.");
        }
    }
}