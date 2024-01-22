using AElf;
using System;
using System.Threading;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Consts;
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
    private readonly IObjectMapper _objectMapper;
    private readonly IPriceDataProvider _provider;
    private readonly IStateProvider _stateProvider;
    private readonly IRetryProvider _retryProvider;
    private readonly IRequestProvider _requestProvider;
    private readonly IDataMessageProvider _dataMessageProvider;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ILogger<CollectObservationJob> _logger;

    public CollectObservationJob(IPeerManager peerManager, ILogger<CollectObservationJob> logger,
        IBackgroundJobManager backgroundJobManager, IObjectMapper objectMapper, IPriceDataProvider provider,
        IRetryProvider retryProvider, IStateProvider stateProvider, IRequestProvider requestProvider,
        IDataMessageProvider dataMessageProvider)
    {
        _logger = logger;
        _provider = provider;
        _peerManager = peerManager;
        _objectMapper = objectMapper;
        _retryProvider = retryProvider;
        _stateProvider = stateProvider;
        _requestProvider = requestProvider;
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
        var requestEpochId = IdGeneratorHelper.GenerateId(chainId, reqId);

        try
        {
            _logger.LogInformation("[Step2] Get leader observation collect request {name}.", argId);
            var currentEpoch = _stateProvider.GetFollowerObservationCurrentEpoch(requestEpochId);
            if (epoch < currentEpoch)
            {
                _logger.LogInformation("[Step2] The epoch in the request {name} is older than the local {epoch}",
                    argId, currentEpoch);
                return;
            }

            var request = await _requestProvider.GetAsync(args);
            if (!await IsJobNeedExecuteAsync(args, request))
            {
                _logger.LogWarning("[Step2] {name} no need to execute", argId);
                return;
            }

            var data = await _dataMessageProvider.GetAsync(args);
            var observationResult = data == null ? await GetDataFeedsDataAsync(request.JobSpec) : data.Data;

            _logger.LogDebug("[step2] Get DataFeeds observation result: {result}", observationResult);

            var ownerIndex = _peerManager.GetOwnIndex();
            var dataMsg = _objectMapper.Map<CollectObservationJobArgs, DataMessageDto>(args);
            dataMsg.Index = ownerIndex;
            dataMsg.Data = observationResult;
            await _dataMessageProvider.SetAsync(dataMsg);

            await ProcessObservationResultAsync(args, observationResult);

            _stateProvider.SetFollowerObservationCurrentEpoch(requestEpochId, args.Epoch);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Step2] {name} Fail.", argId);
            await _retryProvider.RetryAsync(args, backOff: true);
        }
    }

    private async Task<bool> IsJobNeedExecuteAsync(CollectObservationJobArgs args, RequestDto request)
    {
        var argRequestId = args.RequestId;
        var argRoundId = args.RoundId;
        var argEpoch = args.Epoch;

        if (request == null)
        {
            _logger.LogInformation("[Step2] {reqId}-{epoch} is not exist yet.", argRequestId, argEpoch);
            await _retryProvider.RetryAsync(args, backOff: true);
            return false;
        }

        var reqRequestId = request.RequestId;
        var reqEpoch = request.Epoch;
        var reqRoundId = request.RoundId;

        if (argEpoch > reqEpoch || (argEpoch == reqEpoch && request.State is RequestState.RequestEnd))
        {
            _logger.LogInformation("[Step2] {reqId}-{epoch} is not ready yet.", argRequestId, argEpoch);
            await _retryProvider.RetryAsync(args, delayDelta: argEpoch - reqEpoch);
            return false;
        }

        if (request.State is RequestState.RequestCanceled)
        {
            _logger.LogInformation("[Step2] {RequestId} is canceled.", reqRequestId);
            return false;
        }

        if ((argEpoch != reqEpoch || reqRoundId <= argRoundId) && argEpoch >= reqEpoch) return true;

        _logger.LogInformation("[Step2] {RequestId} is not match, epoch:{epoch} round:{Round}.", reqRequestId,
            reqEpoch, reqRoundId);
        return false;
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

        // var context = new CancellationTokenSource(TimeSpan.FromSeconds(GrpcConstants.DefaultRequestTimeout));
        // await _peerManager.CommitToLeaderAsync(p => p.CommitObservationAsync(msg, cancellationToken: context.Token),
        //     args.Epoch, args.RoundId);
        await _peerManager.CommitToLeaderAsync(p => p.CommitObservationAsync(msg), args.Epoch, args.RoundId);
    }
}