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
using Org.BouncyCastle.Utilities.Encoders;
using AElf;

namespace AetherLink.Server.HttpApi.Worker.AELF;

public class RequestSearchWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly AELFOptions _options;
    private readonly JobsReporter _jobsReporter;
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<RequestSearchWorker> _logger;

    public RequestSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<AELFOptions> options, IClusterClient clusterClient, ILogger<RequestSearchWorker> logger,
        JobsReporter jobsReporter) : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _jobsReporter = jobsReporter;
        _clusterClient = clusterClient;
        timer.Period = _options.RequestSearchTimer;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var client = _clusterClient.GetGrain<IAeFinderGrain>(GrainKeyConstants.ConfirmBlockHeightGrainKey);
        var result = await client.GetBlockHeightAsync();
        if (!result.Success)
        {
            _logger.LogWarning("[RequestSearchWorker]Get Block Height failed");
            return;
        }

        await Task.WhenAll(result.Data.Select(d => HandleRequestsAsync(d.ChainId, d.LastIrreversibleBlockHeight)));
    }

    private async Task HandleRequestsAsync(string chainId, long confirmedHeight)
    {
        var grainId =
            GrainIdHelper.GenerateGrainId(GrainKeyConstants.RequestWorkerConsumedBlockHeightGrainKey, chainId);
        var client = _clusterClient.GetGrain<IAELFConsumedBlockHeightGrain>(grainId);
        var consumedHeight = await client.GetConsumedHeightAsync();
        if (!consumedHeight.Success)
        {
            _logger.LogWarning($"[RequestSearchWorker] Get {chainId} consumed block height failed");
            return;
        }

        if (consumedHeight.Data == 0)
        {
            await client.UpdateConsumedHeightAsync(confirmedHeight);
            _logger.LogInformation($"[RequestSearchWorker] Initial {chainId} consumed block height. ");
            return;
        }

        var consumedBlockHeight = consumedHeight.Data + 1;
        if (confirmedHeight < consumedBlockHeight)
        {
            _logger.LogWarning(
                $"[RequestSearchWorker] Waiting for {chainId} block confirmed, consumedBlockHeight:{consumedBlockHeight} confirmedHeight:{confirmedHeight}.");
            return;
        }

        _logger.LogDebug(
            $"[RequestSearchWorker] Get {chainId} Block Height {confirmedHeight}");

        var aeFinderGrainClient = _clusterClient.GetGrain<IAeFinderGrain>(GrainKeyConstants.SearchRampRequestsGrainKey);
        try
        {
            var rampTask = aeFinderGrainClient.SearchRampRequestsAsync(chainId, confirmedHeight, consumedBlockHeight);
            var jobTask = aeFinderGrainClient.SearchOracleJobsAsync(chainId, confirmedHeight, consumedBlockHeight);
            await Task.WhenAll(rampTask, jobTask);

            var rampResult = rampTask.Result;
            var jobResult = jobTask.Result;

            if (!rampResult.Success || !jobResult.Success)
            {
                _logger.LogWarning(
                    $"[RequestSearchWorker] Ramp or Job query failed, will retry this range. ramp:{rampResult.Success}, job:{jobResult.Success}");
                return;
            }

            await HandleRampRequestsAsync(rampResult.Data, chainId);
            await HandleJobTasksAsync(jobResult.Data, chainId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[RequestSearchWorker] Exception in concurrent ramp/job query, will retry this range.");
            return;
        }

        await client.UpdateConsumedHeightAsync(confirmedHeight);
        _logger.LogDebug($"[RequestSearchWorker] {chainId} Block Height consumed at {confirmedHeight}");
    }

    private async Task HandleRampRequestsAsync(List<AELFRampRequestGrainDto> rampRequests, string chainId)
    {
        var tasks = rampRequests.Select(HandleRampRequestAsync);
        await Task.WhenAll(tasks);
        _logger.LogDebug($"[RequestSearchWorker] {chainId} found a total of {rampRequests.Count} ramp requests.");
    }

    private async Task HandleJobTasksAsync(List<AELFJobGrainDto> jobTasks, string chainId)
    {
        _logger.LogDebug($"[RequestSearchWorker] {chainId} found a total of {jobTasks.Count} jobs.");
        foreach (var job in jobTasks)
        {
            var taskType = job.RequestTypeIndex switch
            {
                RequestTypeConst.Datafeeds => StartedRequestTypeName.Datafeeds,
                RequestTypeConst.Vrf => StartedRequestTypeName.Vrf,
                RequestTypeConst.Automation => StartedRequestTypeName.Automation,
                _ => "unknown"
            };
            _jobsReporter.ReportStartedRequest(job.RequestId, chainId, chainId, taskType);
            _logger.LogDebug($"[RequestSearchWorker] {chainId} {taskType} started_request: 1");

            if (job.RequestTypeIndex != RequestTypeConst.Vrf) continue;

            var vrfJobGrain = _clusterClient.GetGrain<IVrfJobGrain>(job.RequestId);
            await vrfJobGrain.UpdateAsync(new VrfJobGrainDto
            {
                ChainId = chainId,
                RequestId = job.RequestId,
                TransactionId = job.TransactionId,
                StartTime = job.StartTime,
                Status = VrfJobStatusConst.Started
            });
        }
    }

    private async Task HandleRampRequestAsync(AELFRampRequestGrainDto data)
    {
        var messageId = data.MessageId;
        if (data.TargetChainId == ChainConstants.TonChainId) messageId = ChangeTonMessageId(data.MessageId);

        _logger.LogDebug($"[RequestSearchWorker] Start to create cross chain request for {messageId}");

        _jobsReporter.ReportStartedRequest(messageId, data.SourceChainId.ToString(), data.TargetChainId.ToString(),
            StartedRequestTypeName.Crosschain);

        var requestGrain = _clusterClient.GetGrain<ICrossChainRequestGrain>(data.TransactionId);
        var result = await requestGrain.UpdateAsync(new()
        {
            Id = messageId,
            SourceChainId = data.SourceChainId,
            TargetChainId = data.TargetChainId,
            MessageId = messageId,
            Status = CrossChainStatus.Started.ToString(),
            StartTime = data.StartTime
        });

        _logger.LogDebug($"[RequestSearchWorker] Update {data.TransactionId} started {result.Success}");

        var transactionIdGrainClient = _clusterClient.GetGrain<ITransactionIdGrain>(messageId);
        var transactionIdUpdateResult =
            await transactionIdGrainClient.UpdateAsync(new() { GrainId = data.TransactionId });

        _logger.LogDebug(
            $"[RequestSearchWorker] Update {data.TransactionId} messageId {messageId} started {transactionIdUpdateResult.Success}");
    }

    private string ChangeTonMessageId(string originMessageId)
    {
        var messageIdBytes = ByteStringHelper.FromHexString(originMessageId).ToByteArray();
        switch (messageIdBytes.Length)
        {
            case > 16:
                messageIdBytes = messageIdBytes.Take(16).ToArray();
                break;
            case < 16:
            {
                var paddedBytes = new byte[16];
                Array.Copy(messageIdBytes, 0, paddedBytes, 16 - messageIdBytes.Length, messageIdBytes.Length);
                messageIdBytes = paddedBytes;
                break;
            }
        }

        return Base64.ToBase64String(messageIdBytes);
    }
}