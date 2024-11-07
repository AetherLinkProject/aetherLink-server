namespace AetherLink.Worker.Core.Dtos;

public class ForwardMessageDto
{
    public string MessageId { get; set; }
    public long SourceChainId { get; set; }
    public long TargetChainId { get; set; }
    public string Sender { get; set; }
    public string Receiver { get; set; }
    public string Message { get; set; }
}