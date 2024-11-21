namespace AetherLink.Server.Grains.State;

[GenerateSerializer]
public class AELFConsumedBlockHeightState
{
    [Id(0)] public string Id { get; set; }
    [Id(1)] public long BlockHeight { get; set; }
}