namespace AetherLink.Server.Grains.State;

[GenerateSerializer]
public class EvmConsumedBlockHeightState
{
    [Id(0)] public string Id { get; set; }
    [Id(1)] public long BlockHeight { get; set; }
}