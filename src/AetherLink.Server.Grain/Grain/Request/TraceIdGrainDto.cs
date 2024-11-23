namespace AetherLink.Server.Grains.Grain.Request;

[GenerateSerializer]
public class TraceIdGrainDto
{
    [Id(0)] public string GrainId { get; set; }
}