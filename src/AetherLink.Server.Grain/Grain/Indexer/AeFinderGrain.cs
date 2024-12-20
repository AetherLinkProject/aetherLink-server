using AetherLink.Indexer.Provider;
using AetherLink.Server.Grains.State;
using Microsoft.Extensions.Logging;

namespace AetherLink.Server.Grains.Grain.Indexer;

public interface IAeFinderGrain : IGrainWithStringKey
{
    Task UpdateConfirmBlockHeightAsync();
    Task<GrainResultDto<List<AELFChainGrainDto>>> GetBlockHeightAsync();

    Task<GrainResultDto<List<AELFRampRequestGrainDto>>> SearchRampRequestsAsync(string chainId, long targetHeight,
        long startHeight);

    Task<GrainResultDto<List<AELFRampRequestGrainDto>>> SearchRequestsCommittedAsync(string chainId, long targetHeight,
        long startHeight);
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
            SourceChainId = r.SourceChainId
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
            SourceChainId = r.SourceChainId
        }).ToList();

        return new() { Data = data };
    }
}