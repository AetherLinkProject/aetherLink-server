using AetherLink.Server.Grains.State;

namespace AetherLink.Server.Grains.Grain.Indexer;



public interface IEvmIndexerGrain : IGrainWithStringKey
{
    Task UpdateConfirmBlockHeightAsync();
    Task<GrainResultDto<List<AELFChainGrainDto>>> GetBlockHeightAsync();

    Task<GrainResultDto<List<AELFRampRequestGrainDto>>> SearchRampRequestsAsync(string chainId, long targetHeight,
        long startHeight);

    Task<GrainResultDto<List<AELFRampRequestGrainDto>>> SearchRequestsCommittedAsync(string chainId, long targetHeight,
        long startHeight);
}

// public class EvmIndexerGrain:Grain<EvmIndexerState>, IEvmIndexerGrain
// {
//  
// }