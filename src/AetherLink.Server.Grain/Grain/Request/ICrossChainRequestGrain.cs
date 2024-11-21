namespace AetherLink.Server.Grains.Grain.Request;

public interface ICrossChainRequestGrain : IGrainWithStringKey
{
    Task<GrainResultDto<CrossChainRequestGrainDto>> GetAsync();
    Task<GrainResultDto<CrossChainRequestGrainDto>> CreateAsync(CrossChainRequestGrainDto input);
    Task<GrainResultDto<CrossChainRequestGrainDto>> UpdateAsync(CrossChainRequestGrainDto input);
}