namespace AetherLink.Server.Grains.State;

public class CrossChainRequestState
{
    public string Id { get; set; }
    public long SourceChainId { get; set; }
    public long TargetChainId { get; set; }
    public string MessageId { get; set; }
    public string Status { get; set; }
    public string LastModifyTime { get; set; }
}