using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Consts;
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
    Task Execute(RequestDto request);
}

public class ObservationCollectSchedulerJob : IObservationCollectSchedulerJob, ITransientDependency
{
    private readonly IPeerManager _peerManager;
    private readonly OracleInfoOptions _options;
    private readonly IObjectMapper _objectMapper;
    private readonly IStateProvider _stateProvider;
    private readonly IReportProvider _reportProvider;
    private readonly IRequestProvider _requestProvider;
    private readonly ILogger<ResetRequestSchedulerJob> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public ObservationCollectSchedulerJob(IObjectMapper objectMapper, ILogger<ResetRequestSchedulerJob> logger,
        IOptionsSnapshot<OracleInfoOptions> options, IStateProvider stateProvider, IRequestProvider requestProvider,
        IReportProvider reportProvider, IPeerManager peerManager, IBackgroundJobManager backgroundJobManager)
    {
        _logger = logger;
        _options = options.Value;
        _peerManager = peerManager;
        _objectMapper = objectMapper;
        _stateProvider = stateProvider;
        _reportProvider = reportProvider;
        _requestProvider = requestProvider;
        _backgroundJobManager = backgroundJobManager;
    }

    public async Task Execute(RequestDto request)
    {
        try
        {
            var chainId = request.ChainId;
            var reqId = request.RequestId;
            var roundId = request.RoundId;
            var epoch = request.Epoch;

            _logger.LogInformation(
                "[ObservationCollectScheduler] Scheduler job execute. reqId {ReqId}, roundId:{RoundId}, reqState:{State}",
                request.RequestId, request.RoundId, request.State.ToString());

            if (!_options.ChainConfig.TryGetValue(request.ChainId, out var chainConfig)) return;
            var reportId = IdGeneratorHelper.GenerateId(request.ChainId, request.RequestId, request.Epoch);

            var observations = _stateProvider.GetPartialObservation(reportId);
            if (observations == null || observations.Count < chainConfig.PartialSignaturesThreshold)
            {
                _logger.LogWarning("[ObservationCollectScheduler] Observation collection not enough.");
                return;
            }

            var collectResult = observations.OrderBy(p => p.Index).Select(p => p.ObservationResult).ToList();
            await _reportProvider.SetAsync(new ReportDto
            {
                ChainId = chainId,
                RequestId = reqId,
                RoundId = roundId,
                Epoch = epoch,
                Observations = collectResult
            });

            // save request.
            request.RoundId = roundId;
            await _requestProvider.SetAsync(request);

            var reportStartSignTime = Timestamp.FromDateTime(DateTime.UtcNow);
            var args = _objectMapper.Map<RequestDto, GeneratePartialSignatureJobArgs>(request);
            args.Observations = collectResult;

            await _backgroundJobManager.EnqueueAsync(args);

            await _peerManager.BroadcastAsync(p => p.CommitReportAsync(new CommitReportRequest
            {
                RequestId = request.RequestId,
                ChainId = request.ChainId,
                RoundId = request.RoundId,
                Epoch = request.Epoch,
                ObservationResults = { collectResult },
                StartTime = reportStartSignTime
            }));

            _stateProvider.SetReportGeneratedFlag(reportId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[ObservationCollectScheduler] Observation collect scheduler execute failed.");
        }
    }
}