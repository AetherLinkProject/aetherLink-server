namespace AetherLink.Server.Grains.Grain.Indexer;

[GenerateSerializer]
public class AELFRampRequestGrainDto
{
    [Id(0)] public string TransactionId { get; set; }
    [Id(1)] public string MessageId { get; set; }
    [Id(2)] public long TargetChainId { get; set; }
    [Id(3)] public long SourceChainId { get; set; }
    [Id(4)] public long StartTime { get; set; }
    [Id(5)] public long CommitTime { get; set; }
}