using JetBrains.Annotations;

namespace AetherLink.Worker.Core.Dtos;

public class ReceiveMessageDto
{
    public string MessageId { get; set; }
    public string Sender { get; set; }
    public long Epoch { get; set; }
    public long SourceChainId { get; set; }
    public long TargetChainId { get; set; }
    public string TargetContractAddress { get; set; }
    public string Message { get; set; }
    [CanBeNull] public TokenAmountDto TokenAmountInfo { get; set; }
}
