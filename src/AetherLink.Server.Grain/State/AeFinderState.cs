using AetherLink.Server.Grains.Grain.Indexer;

namespace AetherLink.Server.Grains.State;

[GenerateSerializer]
public class AeFinderState
{
    [Id(0)] public string Id { get; set; }
    public List<AELFChainGrainDto> ChainItems { get; set; }
}