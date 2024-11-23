using AetherLink.Server.Grains.State;

namespace AetherLink.Server.Grains.Grain.Request;

public interface ITraceIdGrain : IGrainWithStringKey
{
    Task<GrainResultDto<TraceIdGrainDto>> GetAsync();
    Task<GrainResultDto<TraceIdGrainDto>> UpdateAsync(TraceIdGrainDto input);
}

public class TraceIdGrain : Grain<TraceIdState>, ITraceIdGrain
{
    public async Task<GrainResultDto<TraceIdGrainDto>> GetAsync() =>
        new() { Data = new() { GrainId = State.GrainId } };

    public async Task<GrainResultDto<TraceIdGrainDto>> UpdateAsync(TraceIdGrainDto input)
    {
        if (string.IsNullOrEmpty(State.Id)) State.Id = this.GetPrimaryKeyString();

        State.GrainId = input.GrainId;
        await WriteStateAsync();

        return new() { Data = new() { GrainId = State.GrainId } };
    }
}