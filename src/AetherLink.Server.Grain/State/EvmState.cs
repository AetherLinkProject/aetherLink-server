using AetherLink.Server.Grains.Grain.Indexer;

namespace AetherLink.Server.Grains.State;

[GenerateSerializer]
public class EvmState
{
    [Id(0)] public string Id { get; set; }
    [Id(1)] public List<EvmChainGrainDto> ChainItems { get; set; }
}