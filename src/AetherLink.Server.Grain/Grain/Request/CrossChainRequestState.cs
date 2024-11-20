namespace AetherLink.Server.Grains.Grain.Request;

public class CrossChainRequestState
{
    public string Id { get; set; }
    public string TransactionId { get; set; }
    public long TargetChainId { get; set; }
    public long SourceChainId { get; set; }
    public string MessageId { get; set; }
    public string Status { get; set; }
    public string LastModifyTime { get; set; }
}