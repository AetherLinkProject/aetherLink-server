using Volo.Abp.ObjectMapping;

namespace AetherLink.Server.Grains.Grain.Request;

public class CrossChainRequestGrain : Grain<CrossChainRequestState>, ICrossChainRequestGrain
{
    private readonly IObjectMapper _objectMapper;

    public CrossChainRequestGrain(IObjectMapper objectMapper)
    {
        _objectMapper = objectMapper;
    }

    public async Task<GrainResultDto<CrossChainRequestGrainDto>> CreateAsync(CrossChainRequestGrainDto input)
    {
        if (!State.Status.IsNullOrEmpty())
        {
            return new()
            {
                Success = false,
                Message = $"request {input.Id} exists"
            };
        }

        State = _objectMapper.Map<CrossChainRequestGrainDto, CrossChainRequestState>(input);
        if (string.IsNullOrEmpty(State.Id)) State.Id = this.GetPrimaryKey().ToString();

        await WriteStateAsync();

        return new()
        {
            Success = true,
            Data = _objectMapper.Map<CrossChainRequestState, CrossChainRequestGrainDto>(State)
        };
    }

    public async Task<GrainResultDto<CrossChainRequestGrainDto>> UpdateAsync(CrossChainRequestGrainDto input)
    {
        var result = new GrainResultDto<CrossChainRequestGrainDto>();

        State = _objectMapper.Map<CrossChainRequestGrainDto, CrossChainRequestState>(input);
        if (string.IsNullOrEmpty(State.Id)) State.Id = this.GetPrimaryKey().ToString();
        State.LastModifyTime = TimeHelper.GetTimeStampInMilliseconds().ToString();

        await WriteStateAsync();

        result.Data = _objectMapper.Map<CrossChainRequestState, CrossChainRequestGrainDto>(State);
        result.Success = true;
        return result;
    }

    public async Task<GrainResultDto<CrossChainRequestGrainDto>> GetCrossChainTransaction()
    {
        var result = new GrainResultDto<CrossChainRequestGrainDto>();
        if (string.IsNullOrEmpty(State.Id)) return result;
        result.Data = _objectMapper.Map<CrossChainRequestState, CrossChainRequestGrainDto>(State);
        result.Success = true;
        return result;
    }
}