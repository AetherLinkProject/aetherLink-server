using AetherLink.Server.Grains.State;

namespace AetherLink.Server.Grains.Grain.Indexer;

public interface IEvmConsumedBlockHeightGrain : IGrainWithStringKey
{
    Task UpdateConsumedHeightAsync(long blockHeight);
    Task<GrainResultDto<long>> GetConsumedHeightAsync();
}

public class EvmConsumedBlockHeightGrain : Grain<EvmConsumedBlockHeightState>, IEvmConsumedBlockHeightGrain
{
    public async Task UpdateConsumedHeightAsync(long blockHeight)
    {
        if (string.IsNullOrEmpty(State.Id)) State.Id = this.GetPrimaryKeyString();
        State.BlockHeight = blockHeight;
        await WriteStateAsync();
    }

    public async Task<GrainResultDto<long>> GetConsumedHeightAsync() =>
        new() { Success = true, Data = State.BlockHeight };
}