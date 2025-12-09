namespace AetherLink.Server.Grains.Grain.Indexer;

[GenerateSerializer]
public class TransmittedGrainDto
{
    [Id(0)] public string TransactionId { get; set; }
    [Id(1)] public long Epoch { get; set; }
    [Id(2)] public long StartTime { get; set; }
    [Id(3)] public string RequestId { get; set; }
}