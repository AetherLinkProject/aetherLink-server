namespace AetherLink.Server.Grains.Grain.Dtos;

[GenerateSerializer]
public class EvmGrainDto
{
    [Id(0)] public string ChainId { get; set; }
    [Id(1)] public long LastIrreversibleBlockHeight { get; set; }
}