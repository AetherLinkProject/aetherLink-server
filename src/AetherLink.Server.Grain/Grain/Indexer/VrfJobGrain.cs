using AetherLink.Server.Grains.State;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Server.Grains.Grain.Indexer;

public interface IVrfJobGrain : IGrainWithStringKey
{
    Task<GrainResultDto<VrfJobGrainDto>> GetAsync();
    Task<GrainResultDto<VrfJobGrainDto>> UpdateAsync(VrfJobGrainDto input);
}

public class VrfJobGrain : Grain<VrfJobState>, IVrfJobGrain
{
    private readonly IObjectMapper _objectMapper;

    public VrfJobGrain(IObjectMapper objectMapper)
    {
        _objectMapper = objectMapper;
    }

    public async Task<GrainResultDto<VrfJobGrainDto>> GetAsync()
    {
        await ReadStateAsync();
        var result = new GrainResultDto<VrfJobGrainDto>();
        if (string.IsNullOrEmpty(State.RequestId))
        {
            result.Success = false;
            result.Message = "Not found";
            return result;
        }
        result.Data = _objectMapper.Map<VrfJobState, VrfJobGrainDto>(State);
        return result;
    }

    public async Task<GrainResultDto<VrfJobGrainDto>> UpdateAsync(VrfJobGrainDto input)
    {
        State = _objectMapper.Map<VrfJobGrainDto, VrfJobState>(input);
        await WriteStateAsync();
        var result = new GrainResultDto<VrfJobGrainDto>
        {
            Data = _objectMapper.Map<VrfJobState, VrfJobGrainDto>(State)
        };
        return result;
    }
} 