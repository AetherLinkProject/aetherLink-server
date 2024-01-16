using AElf;
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
using Newtonsoft.Json;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.JobPipeline;

public class FollowerObservationProcessJob : AsyncBackgroundJob<FollowerObservationProcessJobArgs>, ISingletonDependency
{
    private readonly IPeerManager _peerManager;
    private readonly IObjectMapper _objectMapper;
    private readonly IPriceDataProvider _provider;
    private readonly IRetryProvider _retryProvider;
    private readonly ISchedulerService _schedulerService;
    private readonly IJobRequestProvider _jobRequestProvider;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ILogger<FollowerObservationProcessJob> _logger;
    private readonly ConcurrentDictionary<string, long> _currentEpochs = new();

    public FollowerObservationProcessJob(IPeerManager peerManager, ILogger<FollowerObservationProcessJob> logger,
        IJobRequestProvider jobRequestProvider, IBackgroundJobManager backgroundJobManager, IObjectMapper objectMapper,
        ISchedulerService schedulerService, IPriceDataProvider provider, IRetryProvider retryProvider)
    {
        _logger = logger;
        _provider = provider;
        _peerManager = peerManager;
        _objectMapper = objectMapper;
        _retryProvider = retryProvider;
        _schedulerService = schedulerService;
        _jobRequestProvider = jobRequestProvider;
        _backgroundJobManager = backgroundJobManager;
    }

    public override async Task ExecuteAsync(FollowerObservationProcessJobArgs args)
    {
        var reqId = args.RequestId;
        var chainId = args.ChainId;
        var roundId = args.RoundId;
        var epoch = args.Epoch;
        var argsName = IdGeneratorHelper.GenerateId(chainId, reqId, epoch, roundId);
        _logger.LogInformation("[Step2] Get leader observation collect request {name}.", argsName);

        try
        {
            if (_currentEpochs.TryGetValue(chainId, out var currentEpoch) && epoch < currentEpoch)
            {
                _logger.LogInformation("[Step2] The epoch in the request {name} is older than the local {epoch}",
                    argsName, currentEpoch);
                return;
            }

            var request = await _jobRequestProvider.GetJobRequestAsync(chainId, reqId, epoch);
            if (request == null || string.IsNullOrEmpty(request.JobSpec))
            {
                _logger.LogInformation("[Step2] Request {name} is not exist yet.", argsName);
                await _retryProvider.RetryAsync(args);
                return;
            }

            if (request.State is RequestState.RequestCanceled or RequestState.RequestEnd)
            {
                _logger.LogInformation("[Step2] Request {name} is canceled.", argsName);
                return;
            }

            if (request.RoundId > args.RoundId)
            {
                _logger.LogInformation("[Step2] Receive {name} round is less than own: {Round}.", argsName,
                    request.RoundId);
                return;
            }

            var managerEpoch = await _peerManager.GetEpochAsync(chainId);
            if (args.Epoch < managerEpoch)
            {
                _logger.LogInformation("[Step2] The epoch in the request {name} is older than the local {epoch}.",
                    argsName, managerEpoch);
                return;
            }

            var data = await _jobRequestProvider.GetDataMessageAsync(chainId, reqId, epoch);
            var observationResult = data == null ? await GetDataFeedsDataAsync(request.JobSpec) : data.Data;
            _logger.LogDebug("[step2] Get DataFeeds observation result: {result}", observationResult);

            var dataMsg = _objectMapper.Map<FollowerObservationProcessJobArgs, DataMessageDto>(args);
            var ownerIndex = _peerManager.GetOwnIndex();
            dataMsg.Index = ownerIndex;
            dataMsg.Data = observationResult;

            await _jobRequestProvider.SetDataMessageAsync(dataMsg);

            request.ObservationResultCommitTime = DateTime.UtcNow;
            request.RequestStartTime = args.RequestStartTime.ToDateTime();
            request.State = RequestState.ObservationResultCommitted;

            await _jobRequestProvider.SetJobRequestAsync(request);

            if (await _peerManager.IsLeaderAsync(chainId, roundId))
            {
                _logger.LogInformation("[Step2][Leader] Save observation result insert queue.");
                var leaderReportJob =
                    _objectMapper.Map<FollowerObservationProcessJobArgs, LeaderGenerateReportJobArgs>(args);
                leaderReportJob.Data = observationResult;
                leaderReportJob.Index = ownerIndex;
                await _backgroundJobManager.EnqueueAsync(leaderReportJob, BackgroundJobPriority.High);
            }
            else
            {
                _logger.LogInformation("[Step2][Follower] Receive leader request {name}.", argsName);
                _schedulerService.CancelScheduler(request, SchedulerType.CheckRequestReceiveScheduler);

                var msg = _objectMapper.Map<FollowerObservationProcessJobArgs, DataMessage>(args);
                msg.Data = observationResult;
                msg.Index = ownerIndex;

                _logger.LogInformation("[Step2][Follower] Response observation result to leader.");
                await _peerManager.RequestLeaderAsync(new StreamMessage
                {
                    MessageType = MessageType.RequestData,
                    RequestId = reqId,
                    Message = msg.ToBytesValue().Value
                }, chainId, roundId);

                // report receive check scheduler
                _logger.LogInformation("[Step2][Follower] {name} Waiting for report from leader", argsName);
                _schedulerService.StartScheduler(request, SchedulerType.CheckReportReceiveScheduler);
            }

            _currentEpochs[chainId] = args.Epoch;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Step2] {name} Fail.", argsName);
        }
    }

    private async Task<long> GetDataFeedsDataAsync(string spec)
    {
        try
        {
            var dataFeedsDto = JsonConvert.DeserializeObject<DataFeedsDto>(spec);
            switch (dataFeedsDto.DataFeedsJobSpec.Type)
            {
                case DataFeedsType.PriceFeeds:
                    return await GetPriceFeedsDataAsync(dataFeedsDto.DataFeedsJobSpec.CurrencyPair);
                default:
                    return 0;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Step2] Invalid job spec.");
            return 0;
        }
    }

    private async Task<long> GetPriceFeedsDataAsync(string currencyPair)
    {
        try
        {
            string[] parts = currencyPair.Split(new[] { "/" }, StringSplitOptions.None);
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
            return 0;
        }
    }
}