namespace AetherLink.Server.Grains.Grain.Indexer;

[GenerateSerializer]
public class AELFChainGrainDto
{
    [Id(0)] public string ChainId { get; set; }
    [Id(1)] public long LastIrreversibleBlockHeight { get; set; }
}