namespace AetherLink.Worker.Core.Dtos;

public class ResendMessageDto
{
    public string MessageId { get; set; }
    public long TargetBlockHeight { get; set; }
    public string Hash { get; set; }
    public long TargetBlockGeneratorTime { get; set; }
    public long ResendTime { get; set; }
    public long CheckCommitTime { get; set; }
}