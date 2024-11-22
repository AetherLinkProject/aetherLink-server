using AetherLink.Server.Grains.State;

namespace AetherLink.Server.Grains.Grain.Indexer;

public interface ITransactionIdGrain : IGrainWithStringKey
{
    Task<GrainResultDto<TransactionIdGrainDto>> GetAsync();
    Task<GrainResultDto<TransactionIdGrainDto>> UpdateAsync(TransactionIdGrainDto input);
}

public class TransactionIdGrain : Grain<TransactionIdState>, ITransactionIdGrain
{
    public async Task<GrainResultDto<TransactionIdGrainDto>> GetAsync() =>
        new() { Data = new() { GrainId = State.GrainId } };

    public async Task<GrainResultDto<TransactionIdGrainDto>> UpdateAsync(TransactionIdGrainDto input)
    {
        if (string.IsNullOrEmpty(State.Id)) State.Id = this.GetPrimaryKeyString();

        State.GrainId = input.GrainId;
        await WriteStateAsync();

        return new() { Data = new() { GrainId = State.GrainId } };
    }
}