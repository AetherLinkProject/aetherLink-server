using AetherLink.Indexer.Provider;
using AetherLink.Server.Grains.State;

namespace AetherLink.Server.Grains.Grain.Indexer;

public class AeFinderGrain : Grain<AeFinderState>, IAeFinderGrain
{
    private readonly IAeFinderProvider _indexer;

    public AeFinderGrain(IAeFinderProvider indexer)
    {
        _indexer = indexer;
    }

    public async Task UpdateConfirmBlockHeightAsync()
    {
        if (string.IsNullOrEmpty(State.Id)) State.Id = this.GetPrimaryKeyString();

        var chainStates = await _indexer.GetChainSyncStateAsync();

        var aeFinderBlockHeightGrain = GrainFactory.GetGrain<IAELFBlockHeightGrain>("AeFinderBlockHeight");
        await aeFinderBlockHeightGrain.UpdateHeightAsync(chainStates);
        // State.LastModifyTime = TimeHelper.GetTimeStampInMilliseconds().ToString();
        // GrainFactory
        await WriteStateAsync();

        // var data = _objectMapper.Map<CrossChainRequestState, CrossChainRequestGrainDto>(State);
    }
}