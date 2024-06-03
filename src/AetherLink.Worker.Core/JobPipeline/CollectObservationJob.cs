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
    private readonly IPriceDataProvider _provider;
    private readonly IRetryProvider _retryProvider;
    private readonly ILogger<CollectObservationJob> _logger;
    private readonly IDataMessageProvider _dataMessageProvider;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public CollectObservationJob(IPeerManager peerManager, ILogger<CollectObservationJob> logger,
        IBackgroundJobManager backgroundJobManager, IObjectMapper objectMapper, IPriceDataProvider provider,
        IRetryProvider retryProvider, IJobProvider jobProvider, IDataMessageProvider dataMessageProvider)
    {
        _logger = logger;
        _provider = provider;
        _peerManager = peerManager;
        _jobProvider = jobProvider;
        _objectMapper = objectMapper;
        _retryProvider = retryProvider;
        _dataMessageProvider = dataMessageProvider;
        _backgroundJobManager = backgroundJobManager;
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
            if (!await IsJobNeedExecuteAsync(args, job))
            {
                _logger.LogDebug("[Step2] {name} no need to execute", argId);
                return;
            }

            var data = await _dataMessageProvider.GetAsync(args);
            var observationResult = data == null ? await GetDataFeedsDataAsync(job.JobSpec) : data.Data;

            _logger.LogDebug("[step2] Get DataFeeds observation result: {result}", observationResult);

            var dataMsg = _objectMapper.Map<CollectObservationJobArgs, DataMessageDto>(args);
            dataMsg.Data = observationResult;
            await _dataMessageProvider.SetAsync(dataMsg);

            await ProcessObservationResultAsync(args, observationResult);
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

    private async Task<long> GetDataFeedsDataAsync(string spec)
    {
        try
        {
            var dataFeedsDto = JsonConvert.DeserializeObject<DataFeedsDto>(spec);
            return dataFeedsDto.DataFeedsJobSpec.Type switch
            {
                DataFeedsType.PriceFeeds => await GetPriceFeedsDataAsync(dataFeedsDto.DataFeedsJobSpec.CurrencyPair),
                _ => 0
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Step2] Invalid job spec.");
            throw;
        }
    }

    private async Task<long> GetPriceFeedsDataAsync(string currencyPair)
    {
        try
        {
            var parts = currencyPair.Split(new[] { "/" }, StringSplitOptions.None);
            var symbol = parts[0];
            var vsCurrency = parts[1];
            _logger.LogInformation("[step2] PriceFeeds search {symbol}:{currency}", symbol, vsCurrency);

            return await _provider.GetPriceAsync(new PriceDataDto
            {
                BaseCurrency = symbol.ToUpper(),
                QuoteCurrency = vsCurrency.ToUpper()
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Step2] Get price feed data failed.");
            throw;
        }
    }

    private async Task ProcessObservationResultAsync(CollectObservationJobArgs args, long result)
    {
        if (_peerManager.IsLeader(args.Epoch, args.RoundId))
        {
            var leaderReportJob =
                _objectMapper.Map<CollectObservationJobArgs, GenerateReportJobArgs>(args);
            leaderReportJob.Data = result;
            leaderReportJob.Index = _peerManager.GetOwnIndex();
            await _backgroundJobManager.EnqueueAsync(leaderReportJob, BackgroundJobPriority.High);
            return;
        }

        var msg = _objectMapper.Map<CollectObservationJobArgs, CommitObservationRequest>(args);
        msg.Data = result;
        msg.Index = _peerManager.GetOwnIndex();
        await _peerManager.CommitToLeaderAsync(p => p.CommitObservationAsync(msg), args.Epoch,
            args.RoundId);
    }
}