using AetherLink.Indexer.Provider;
using AetherLink.Server.Grains.State;
using Microsoft.Extensions.Logging;
using AetherLink.Indexer.Dtos;

namespace AetherLink.Server.Grains.Grain.Indexer;

public interface IAeFinderGrain : IGrainWithStringKey
{
    Task UpdateConfirmBlockHeightAsync();
    Task<GrainResultDto<List<AELFChainGrainDto>>> GetBlockHeightAsync();

    Task<GrainResultDto<List<AELFRampRequestGrainDto>>> SearchRampRequestsAsync(string chainId, long targetHeight,
        long startHeight);

    Task<GrainResultDto<List<AELFRampRequestGrainDto>>> SearchRequestsCommittedAsync(string chainId, long targetHeight,
        long startHeight);

    Task<GrainResultDto<List<AELFJobGrainDto>>> SearchOracleJobsAsync(string chainId, long targetHeight,
        long startHeight);

    Task<GrainResultDto<List<TransmittedDto>>> SubscribeTransmittedAsync(string chainId, long targetHeight, long startHeight);
}

public class AeFinderGrain : Grain<AeFinderState>, IAeFinderGrain
{
    private readonly IAeFinderProvider _indexer;
    private readonly ILogger<AeFinderGrain> _logger;

    public AeFinderGrain(IAeFinderProvider indexer, ILogger<AeFinderGrain> logger)
    {
        _logger = logger;
        _indexer = indexer;
    }

    public async Task UpdateConfirmBlockHeightAsync()
    {
        if (string.IsNullOrEmpty(State.Id)) State.Id = this.GetPrimaryKeyString();

        var chainStates = await _indexer.GetChainSyncStateAsync();
        var chainItems = chainStates.Select(c => new AELFChainGrainDto
        {
            ChainId = c.ChainId,
            LastIrreversibleBlockHeight = c.LastIrreversibleBlockHeight
        }).ToList();

        foreach (var item in chainItems)
        {
            _logger.LogDebug($"Updated {item.ChainId} index height to {item.LastIrreversibleBlockHeight}");
        }

        State.ChainItems = chainItems;
        await WriteStateAsync();
    }

    public async Task<GrainResultDto<List<AELFChainGrainDto>>> GetBlockHeightAsync()
        => new() { Success = true, Data = State.ChainItems };

    public async Task<GrainResultDto<List<AELFRampRequestGrainDto>>> SearchRampRequestsAsync(string chainId,
        long targetHeight, long startHeight)
    {
        var result = await _indexer.SubscribeRampRequestsAsync(chainId, targetHeight, startHeight);
        if (result == null) return new() { Success = false, Message = "Search failed" };
        if (result.Count == 0) return new() { Data = new(), Message = "Empty data" };
        var data = result.Select(r => new AELFRampRequestGrainDto
        {
            TransactionId = r.TransactionId,
            MessageId = r.MessageId,
            TargetChainId = r.TargetChainId,
            SourceChainId = r.SourceChainId,
            StartTime = r.StartTime
        }).ToList();

        return new() { Data = data };
    }

    public async Task<GrainResultDto<List<AELFRampRequestGrainDto>>> SearchRequestsCommittedAsync(string chainId,
        long targetHeight, long startHeight)
    {
        var result = await _indexer.SubscribeRampCommitReportAsync(chainId, targetHeight, startHeight);
        if (result == null) return new() { Success = false, Message = "Search failed" };
        if (result.Count == 0) return new() { Data = new(), Message = "Empty data" };
        var data = result.Select(r => new AELFRampRequestGrainDto
        {
            TransactionId = r.TransactionId,
            MessageId = r.MessageId,
            TargetChainId = r.TargetChainId,
            SourceChainId = r.SourceChainId,
            CommitTime = r.CommitTime
        }).ToList();

        return new() { Data = data };
    }

    public async Task<GrainResultDto<List<AELFJobGrainDto>>> SearchOracleJobsAsync(string chainId, long targetHeight,
        long startHeight)
    {
        try
        {
            var ocrJobEvents = await _indexer.SubscribeLogsAsync(chainId, targetHeight, startHeight);
            var jobs = ocrJobEvents.Select(e => new AELFJobGrainDto
            {
                RequestTypeIndex = e.RequestTypeIndex,
                TransactionId = e.TransactionId,
                BlockHeight = e.BlockHeight,
                BlockHash = e.BlockHash,
                StartTime = e.StartTime,
                RequestId = e.RequestId
            }).ToList();
            return new() { Data = jobs, Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"[AeFinderGrain] SearchOracleJobsAsync failed for {chainId} {startHeight}-{targetHeight}");
            return new() { Data = new List<AELFJobGrainDto>(), Success = false, Message = ex.Message };
        }
    }

    public async Task<GrainResultDto<List<TransmittedDto>>> SubscribeTransmittedAsync(string chainId, long targetHeight, long startHeight)
    {
        var result = await _indexer.SubscribeTransmittedAsync(chainId, targetHeight, startHeight);
        if (result == null) return new() { Success = false, Message = "Search failed" };
        if (result.Count == 0) return new() { Data = new(), Message = "Empty data" };
        return new() { Data = result };
    }
}