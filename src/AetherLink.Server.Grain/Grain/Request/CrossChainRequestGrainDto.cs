namespace AetherLink.Server.Grains.Grain.Request;

public class CrossChainRequestGrainDto
{
    public string Id { get; set; }
    public long SourceChainId { get; set; }
    public long TargetChainId { get; set; }
    public string MessageId { get; set; }
    public string Status { get; set; }
    public string LastModifyTime { get; set; }
}