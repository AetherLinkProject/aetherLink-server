namespace AetherLink.Server.Grains.State;

[GenerateSerializer]
public class VrfJobState
{
    [Id(0)] public string ChainId { get; set; }
    [Id(1)] public string RequestId { get; set; }
    [Id(2)] public string TransactionId { get; set; }
    [Id(3)] public long StartTime { get; set; }
    [Id(4)] public long CommitTime { get; set; }
    [Id(5)] public string Status { get; set; }
} 