namespace AetherLink.Server.Grains.State;

[GenerateSerializer]
public class TraceIdState
{
    [Id(0)] public string Id { get; set; }
    [Id(1)] public string GrainId { get; set; }
}