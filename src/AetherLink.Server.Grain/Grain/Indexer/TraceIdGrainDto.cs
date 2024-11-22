namespace AetherLink.Server.Grains.Grain.Indexer;

[GenerateSerializer]
public class TraceIdGrainDto
{
    [Id(0)] public string GrainId { get; set; }
}