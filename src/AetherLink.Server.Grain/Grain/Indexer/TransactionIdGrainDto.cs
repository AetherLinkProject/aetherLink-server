namespace AetherLink.Server.Grains.Grain.Indexer;

[GenerateSerializer]
public class TransactionIdGrainDto
{
    [Id(0)] public string GrainId { get; set; }
}