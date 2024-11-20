namespace AetherLink.Server.Grains.Grain.Request;

public interface ICrossChainRequestGrain : IGrainWithGuidKey
{
    Task<GrainResultDto<CrossChainRequestGrainDto>> CreateAsync(CrossChainRequestGrainDto input);
    Task<GrainResultDto<CrossChainRequestGrainDto>> UpdateAsync(CrossChainRequestGrainDto input);
    Task<GrainResultDto<CrossChainRequestGrainDto>> GetCrossChainTransaction();
}