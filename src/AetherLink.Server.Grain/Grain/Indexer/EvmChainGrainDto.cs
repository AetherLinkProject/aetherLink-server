namespace AetherLink.Server.Grains.Grain.Indexer;

[GenerateSerializer]
public class EvmChainGrainDto
{
    [Id(0)] public string NetworkName { get; set; }
    [Id(1)] public long ConsumedBlockHeight { get; set; }
}