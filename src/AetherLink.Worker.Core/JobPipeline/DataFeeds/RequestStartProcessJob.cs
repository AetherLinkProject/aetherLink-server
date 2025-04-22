using System;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.PeerManager;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.JobPipeline.DataFeeds;

public class RequestStartProcessJob : AsyncBackgroundJob<RequestStartProcessJobArgs>, ITransientDependency
{
    private readonly IPeerManager _peerManager;
    private readonly IJobProvider _jobProvider;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<RequestStartProcessJob> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public RequestStartProcessJob(IPeerManager peerManager, ILogger<RequestStartProcessJob> logger,
        IBackgroundJobManager backgroundJobManager, IObjectMapper objectMapper, IJobProvider jobProvider)
    {
        _logger = logger;
        _peerManager = peerManager;
        _jobProvider = jobProvider;
        _objectMapper = objectMapper;
        _backgroundJobManager = backgroundJobManager;
    }

    public override async Task ExecuteAsync(RequestStartProcessJobArgs args)
    {
        var argId = IdGeneratorHelper.GenerateId(args.ChainId, args.RequestId, args.Epoch, args.RoundId);

        try
        {
            var job = await _jobProvider.GetAsync(args);
            if (job == null)
            {
                job = _objectMapper.Map<RequestStartProcessJobArgs, JobDto>(args);
                job.RequestReceiveTime = DateTimeOffset.FromUnixTimeMilliseconds(args.StartTime).DateTime;
            }
            else if (job.State == RequestState.RequestCanceled || args.Epoch < job.Epoch) return;

            var currentRoundId = _peerManager.GetCurrentRoundId(job.RequestReceiveTime);
            job.RoundId = currentRoundId;
            job.State = RequestState.RequestStart;
            await _jobProvider.SetAsync(job);

            _logger.LogDebug("[Step1] {name} start startTime {time}, blockTime {blockTime}", argId, args.StartTime,
                job.TransactionBlockTime);

            if (_peerManager.IsLeader(args.Epoch, currentRoundId))
            {
                _logger.LogInformation("[Step1][Leader] {name} Is Leader.", argId);
                await _backgroundJobManager.EnqueueAsync(
                    _objectMapper.Map<RequestStartProcessJobArgs, CollectObservationJobArgs>(args),
                    BackgroundJobPriority.High);

                await _peerManager.BroadcastAsync(p => p.QueryObservationAsync(new QueryObservationRequest
                {
                    RequestId = args.RequestId,
                    ChainId = args.ChainId,
                    RoundId = currentRoundId,
                    Epoch = args.Epoch
                }));
            }

            _logger.LogInformation("[step1] {name} Waiting for request end.", argId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[step1] {name} RequestStart process failed.", argId);
        }
    }
}