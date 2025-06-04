namespace AetherLink.Server.Grains.State;

[GenerateSerializer]
public class CrossChainRequestState
{
    [Id(0)] public string Id { get; set; }
    [Id(1)] public long SourceChainId { get; set; }
    [Id(2)] public long TargetChainId { get; set; }
    [Id(3)] public string MessageId { get; set; }
    [Id(4)] public string Status { get; set; }
    [Id(5)] public long StartTime { get; set; } // 任务开始时间（毫秒）
}