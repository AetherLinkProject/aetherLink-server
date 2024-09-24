using System;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.PeerManager;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Newtonsoft.Json;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.JobPipeline;

public class CollectObservationJob : AsyncBackgroundJob<CollectObservationJobArgs>, ITransientDependency
{
    private readonly IPeerManager _peerManager;
    private readonly IJobProvider _jobProvider;
    private readonly IObjectMapper _objectMapper;
    private readonly IRetryProvider _retryProvider;
    private readonly ILogger<CollectObservationJob> _logger;
    private readonly IPriceFeedsProvider _priceFeedsProvider;
    private readonly IDataMessageProvider _dataMessageProvider;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IPlainDataFeedsProvider _plainDataFeedsProvider;

    public CollectObservationJob(IPeerManager peerManager, IJobProvider jobProvider, IObjectMapper objectMapper,
        IRetryProvider retryProvider, ILogger<CollectObservationJob> logger, IPriceFeedsProvider priceFeedsProvider,
        IDataMessageProvider dataMessageProvider, IBackgroundJobManager backgroundJobManager,
        IPlainDataFeedsProvider plainDataFeedsProvider)
    {
        _logger = logger;
        _peerManager = peerManager;
        _jobProvider = jobProvider;
        _objectMapper = objectMapper;
        _retryProvider = retryProvider;
        _priceFeedsProvider = priceFeedsProvider;
        _dataMessageProvider = dataMessageProvider;
        _backgroundJobManager = backgroundJobManager;
        _plainDataFeedsProvider = plainDataFeedsProvider;
    }

    public override async Task ExecuteAsync(CollectObservationJobArgs args)
    {
        var chainId = args.ChainId;
        var reqId = args.RequestId;
        var epoch = args.Epoch;
        var roundId = args.RoundId;
        var argId = IdGeneratorHelper.GenerateId(chainId, reqId, epoch, roundId);

        try
        {
            _logger.LogInformation("[Step2] Get leader observation collect job {name}.", argId);

            var job = await _jobProvider.GetAsync(args);
            if (!await IsJobNeedExecuteAsync(args, job)) return;

            var dataSpec = JsonConvert.DeserializeObject<DataFeedsDto>(job.JobSpec).DataFeedsJobSpec;
            var observationResult = await GetDataFeedsDataAsync(args, dataSpec);
            _logger.LogDebug("[step2] Get DataFeeds observation result: {result}", observationResult);

            if (string.IsNullOrEmpty(observationResult))
            {
                // The timeout window still exists and the collection will be reorganized in the next round.
                _logger.LogWarning("[step2] Empty collection results will not be returned to the leader.");
                return;
            }

            if (_peerManager.IsLeader(args.Epoch, args.RoundId))
            {
                var leaderJob = _objectMapper.Map<CollectObservationJobArgs, GenerateReportJobArgs>(args);
                if (dataSpec.Type == DataFeedsType.PriceFeeds) leaderJob.Data = observationResult;
                leaderJob.Index = _peerManager.GetOwnIndex();
                await _backgroundJobManager.EnqueueAsync(leaderJob, BackgroundJobPriority.High);
                return;
            }

            var msg = _objectMapper.Map<CollectObservationJobArgs, CommitObservationRequest>(args);
            if (dataSpec.Type == DataFeedsType.PriceFeeds) msg.Data = observationResult;
            msg.Index = _peerManager.GetOwnIndex();
            await _peerManager.CommitToLeaderAsync(p => p.CommitObservationAsync(msg), args.Epoch, args.RoundId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Step2] {name} Fail.", argId);
            await _retryProvider.RetryAsync(args, backOff: true);
        }
    }

    private async Task<bool> IsJobNeedExecuteAsync(CollectObservationJobArgs args, JobDto job)
    {
        var argRequestId = args.RequestId;
        var argRoundId = args.RoundId;
        var argEpoch = args.Epoch;

        if (job == null)
        {
            _logger.LogInformation("[Step2] {reqId}-{epoch} is not exist yet.", argRequestId, argEpoch);
            await _retryProvider.RetryAsync(args, backOff: true);
            return false;
        }

        var reqRequestId = job.RequestId;
        var reqEpoch = job.Epoch;
        var reqRoundId = job.RoundId;

        if (argEpoch > reqEpoch || (argEpoch == reqEpoch && job.State is RequestState.RequestEnd))
        {
            _logger.LogInformation("[Step2] {reqId}-{epoch} is not ready yet.", argRequestId, argEpoch);
            await _retryProvider.RetryAsync(args, delayDelta: argEpoch - reqEpoch);
            return false;
        }

        if (job.State is RequestState.RequestCanceled)
        {
            _logger.LogInformation("[Step2] {RequestId} is canceled.", reqRequestId);
            return false;
        }

        if (reqRoundId > argRoundId || argEpoch < reqEpoch)
        {
            _logger.LogInformation("[Step2] {RequestId} is not match, epoch:{epoch} round:{Round}.", reqRequestId,
                reqEpoch, reqRoundId);
            return false;
        }

        return true;
    }

    private async Task<string> GetDataFeedsDataAsync(CollectObservationJobArgs args, DataFeedsJobSpec dataSpec)
    {
        try
        {
            switch (dataSpec.Type)
            {
                case DataFeedsType.PriceFeeds:
                    _logger.LogInformation("[Step2] Starting execute PriceFeeds collect job.");
                    var priceData = await _dataMessageProvider.GetAsync(args);
                    if (priceData != null) return priceData.Data.ToString();

                    var amount = await _priceFeedsProvider.GetPriceFeedsDataAsync(dataSpec.CurrencyPair);
                    var dataMsg = _objectMapper.Map<CollectObservationJobArgs, DataMessageDto>(args);
                    dataMsg.Data = amount;
                    await _dataMessageProvider.SetAsync(dataMsg);
                    return amount.ToString();
                case DataFeedsType.PlainDataFeeds:
                    _logger.LogInformation("[Step2] Starting execute PlainDataFeedsDto collect job.");
                    var resp = await _plainDataFeedsProvider.RequestPlainDataAsync(dataSpec.Url);
                    if (string.IsNullOrEmpty(resp)) return "";

                    var plainData = await _dataMessageProvider.GetPlainDataFeedsAsync(args);

                    if (plainData == null)
                    {
                        plainData = _objectMapper.Map<CollectObservationJobArgs, PlainDataFeedsDto>(args);
                        plainData.OldData = "";
                    }
                    else if (plainData.OldData != null && resp == plainData.OldData)
                    {
                        _logger.LogDebug("[Step2] New collect result is same as old data {data}", resp);
                        return "";
                    }

                    plainData.NewData = resp;
                    await _dataMessageProvider.SetAsync(plainData);
                    return resp;
                default:
                    return "";
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Step2] Get DataFeeds data failed.");
            throw;
        }
    }
}