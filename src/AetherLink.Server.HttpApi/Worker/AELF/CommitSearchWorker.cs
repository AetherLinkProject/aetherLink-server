using AElf;
using AetherLink.Server.Grains;
using AetherLink.Server.Grains.Grain.Indexer;
using AetherLink.Server.Grains.Grain.Request;
using AetherLink.Server.HttpApi.Constants;
using AetherLink.Server.HttpApi.Dtos;
using AetherLink.Server.HttpApi.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using AetherLink.Server.HttpApi.Reporter;

namespace AetherLink.Server.HttpApi.Worker.AELF;

public class CommitSearchWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly AELFOptions _options;
    private readonly JobsReporter _jobsReporter;
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<CommitSearchWorker> _logger;

    public CommitSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<AELFOptions> options, IClusterClient clusterClient, ILogger<CommitSearchWorker> logger,
        JobsReporter jobsReporter) : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _jobsReporter = jobsReporter;
        _clusterClient = clusterClient;
        timer.Period = _options.CommitSearchTimer;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var client = _clusterClient.GetGrain<IAeFinderGrain>(GrainKeyConstants.ConfirmBlockHeightGrainKey);
        var result = await client.GetBlockHeightAsync();
        if (!result.Success)
        {
            _logger.LogWarning("[CommitSearchWorker] Get Block Height failed");
            return;
        }

        await Task.WhenAll(result.Data.Select(ProcessChainTaskAsync));
    }

    private async Task ProcessChainTaskAsync(AELFChainGrainDto chain)
    {
        try
        {
            var chainId = chain.ChainId;
            var confirmedHeight = chain.LastIrreversibleBlockHeight;
            var grainId =
                GrainIdHelper.GenerateGrainId(GrainKeyConstants.CommitWorkerConsumedBlockHeightGrainKey, chainId);
            var consumedClient = _clusterClient.GetGrain<IAELFConsumedBlockHeightGrain>(grainId);
            var consumedHeight = await consumedClient.GetConsumedHeightAsync();
            if (!consumedHeight.Success)
            {
                _logger.LogWarning($"[CommitSearchWorker] Get {chainId} consumed block height failed");
                return;
            }

            if (consumedHeight.Data == 0)
            {
                await consumedClient.UpdateConsumedHeightAsync(confirmedHeight);
                _logger.LogInformation($"[CommitSearchWorker] Initial {chainId} consumed block height. ");
                return;
            }

            var consumedBlockHeight = consumedHeight.Data + 1;
            if (confirmedHeight < consumedBlockHeight)
            {
                _logger.LogWarning(
                    $"[CommitSearchWorker] Waiting for {chainId} block confirmed, consumedBlockHeight:{consumedBlockHeight} confirmedHeight:{confirmedHeight}.");
                return;
            }

            await HandleRequestsAsync(chain, confirmedHeight, consumedBlockHeight, consumedClient);
            await HandleVrfCommitsAsync(chain, confirmedHeight, consumedBlockHeight);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[CommitSearchWorker] Exception occurred while processing chain {chain.ChainId}");
        }
    }

    private async Task HandleRequestsAsync(AELFChainGrainDto chain, long confirmedHeight, long consumedBlockHeight,
        IAELFConsumedBlockHeightGrain consumedClient)
    {
        var chainId = chain.ChainId;
        _logger.LogDebug($"[CommitSearchWorker] Get {chainId} Block Height {confirmedHeight}");
        var aeFinderGrainClient =
            _clusterClient.GetGrain<IAeFinderGrain>(GrainKeyConstants.SearchRequestsCommittedGrainKey);
        var requests =
            await aeFinderGrainClient.SearchRequestsCommittedAsync(chainId, confirmedHeight, consumedBlockHeight);
        if (!requests.Success)
        {
            _logger.LogError($"[CommitSearchWorker] {chainId} Get requests failed");
            return;
        }

        var tasks = requests.Data.Select(HandleReportCommittedAsync);
        await Task.WhenAll(tasks);

        _logger.LogInformation("[CommitSearchWorker] {chain} found a total of {count} committed report.", chainId,
            requests.Data.Count);
        await consumedClient.UpdateConsumedHeightAsync(confirmedHeight);

        _logger.LogDebug($"[CommitSearchWorker] {chainId} Block Height consumed at {confirmedHeight}");
    }

    private async Task HandleVrfCommitsAsync(AELFChainGrainDto chain, long confirmedHeight, long consumedBlockHeight)
    {
        var aeFinderGrainClient =
            _clusterClient.GetGrain<IAeFinderGrain>(GrainKeyConstants.SearchRequestsCommittedGrainKey);
        var jobsResult =
            await aeFinderGrainClient.SubscribeTransmittedAsync(chain.ChainId, confirmedHeight, consumedBlockHeight);
        if (jobsResult.Success && jobsResult.Data != null)
        {
            foreach (var job in jobsResult.Data)
            {
                var vrfJobGrain = _clusterClient.GetGrain<IVrfJobGrain>(job.RequestId);
                var vrfJob = await vrfJobGrain.GetAsync();
                if (vrfJob?.Data == null || vrfJob.Data.CommitTime > 0 || job.StartTime <= 0 ||
                    vrfJob.Data.StartTime <= 0)
                {
                    _logger.LogDebug(
                        $"[CommitSearchWorker] VRF RequestId={job.RequestId} does not meet processing conditions, skip");
                    continue;
                }

                var duration = (job.StartTime - vrfJob.Data.StartTime) / 1000.0;
                _logger.LogInformation(
                    $"[CommitSearchWorker] VRF RequestId={job.RequestId}, Duration={duration}s (from job.StartTime)");
                _jobsReporter.ReportExecutionDuration(chain.ChainId, StartedRequestTypeName.Vrf, duration);
                vrfJob.Data.CommitTime = job.StartTime;
                await vrfJobGrain.UpdateAsync(vrfJob.Data);
            }
        }
    }

    private async Task HandleReportCommittedAsync(AELFRampRequestGrainDto requestData)
    {
        var requestGrain = _clusterClient.GetGrain<ICrossChainRequestGrain>(requestData.MessageId);
        var messageId = ByteStringHelper.FromHexString(requestData.MessageId).ToBase64();
        var result = await requestGrain.GetAsync();
        if (!result.Success || result.Data == null)
        {
            _logger.LogError(
                $"[CommitSearchWorker] GetAsync failed or returned null Data for messageId: {requestData.MessageId}, Success: {result.Success}");
            return;
        }

        if (result.Data.Status == CrossChainStatus.Committed.ToString())
        {
            _logger.LogInformation(
                $"[CommitSearchWorker] MessageId {requestData.MessageId} already committed, skip duration report.");
            return;
        }

        _logger.LogInformation(
            $"[CommitSearchWorker] Reporting committed for MessageId {requestData.MessageId}, ChainId {requestData.SourceChainId}");

        _jobsReporter.ReportCommittedReport(requestData.SourceChainId.ToString(), StartedRequestTypeName.Crosschain);

        var duration = (requestData.CommitTime - result.Data.StartTime) / 1000.0;

        _logger.LogInformation(
            $"[CommitSearchWorker] ReportExecutionDuration: MessageId={requestData.MessageId}, ChainId={requestData.SourceChainId}, Duration={duration}s");

        _jobsReporter.ReportExecutionDuration(requestData.SourceChainId.ToString(), StartedRequestTypeName.Crosschain,
            duration);

        var updateResult = await requestGrain.UpdateAsync(new()
        {
            Id = messageId,
            SourceChainId = requestData.SourceChainId,
            TargetChainId = requestData.TargetChainId,
            MessageId = messageId,
            Status = CrossChainStatus.Committed.ToString(),
            StartTime = requestData.StartTime,
            CommitTime = requestData.CommitTime
        });

        _logger.LogDebug($"[CommitSearchWorker] Update {requestData.MessageId} committed {updateResult.Success}");
    }
}