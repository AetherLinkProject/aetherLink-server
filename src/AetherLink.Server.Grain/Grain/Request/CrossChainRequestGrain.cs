using AetherLink.Server.Grains.State;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Server.Grains.Grain.Request;

public interface ICrossChainRequestGrain : IGrainWithStringKey
{
    Task<GrainResultDto<CrossChainRequestGrainDto>> GetAsync();
    Task<GrainResultDto<CrossChainRequestGrainDto>> CreateAsync(CrossChainRequestGrainDto input);
    Task<GrainResultDto<CrossChainRequestGrainDto>> UpdateAsync(CrossChainRequestGrainDto input);
}

public class CrossChainRequestGrain : Grain<CrossChainRequestState>, ICrossChainRequestGrain
{
    private readonly IObjectMapper _objectMapper;

    public CrossChainRequestGrain(IObjectMapper objectMapper)
    {
        _objectMapper = objectMapper;
    }

    public async Task<GrainResultDto<CrossChainRequestGrainDto>> GetAsync()
    {
        await ReadStateAsync();

        var result = new GrainResultDto<CrossChainRequestGrainDto>();
        if (string.IsNullOrEmpty(State.Id)) return result;
        var data = _objectMapper.Map<CrossChainRequestState, CrossChainRequestGrainDto>(State);
        return new() { Success = true, Data = data };
    }

    public async Task<GrainResultDto<CrossChainRequestGrainDto>> CreateAsync(CrossChainRequestGrainDto input)
    {
        if (!State.Status.IsNullOrEmpty()) return new() { Message = $"request {input.Id} exists" };

        State = _objectMapper.Map<CrossChainRequestGrainDto, CrossChainRequestState>(input);
        if (string.IsNullOrEmpty(State.Id)) State.Id = this.GetPrimaryKeyString();

        await WriteStateAsync();

        var data = _objectMapper.Map<CrossChainRequestState, CrossChainRequestGrainDto>(State);
        return new() { Success = true, Data = data };
    }

    public async Task<GrainResultDto<CrossChainRequestGrainDto>> UpdateAsync(CrossChainRequestGrainDto input)
    {
        State = _objectMapper.Map<CrossChainRequestGrainDto, CrossChainRequestState>(input);
        if (string.IsNullOrEmpty(State.Id)) State.Id = this.GetPrimaryKeyString();
        State.LastModifyTime = TimeHelper.GetTimeStampInMilliseconds().ToString();

        await WriteStateAsync();

        var data = _objectMapper.Map<CrossChainRequestState, CrossChainRequestGrainDto>(State);
        return new() { Success = true, Data = data };
    }
}