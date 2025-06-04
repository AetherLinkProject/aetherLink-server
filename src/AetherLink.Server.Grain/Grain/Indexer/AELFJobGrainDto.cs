namespace AetherLink.Server.Grains.Grain.Indexer;

[GenerateSerializer]
public class AELFJobGrainDto
{
    [Id(0)] public int RequestTypeIndex { get; set; }
    [Id(1)] public string TransactionId { get; set; }
    [Id(2)] public long BlockHeight { get; set; }
    [Id(3)] public string BlockHash { get; set; }
    [Id(4)] public long StartTime { get; set; }
    [Id(5)] public string RequestId { get; set; }
} 