using Hangfire.Annotations;
using Nethereum.Hex.HexTypes;

namespace AetherLink.Worker.Core.Dtos;

public class EvmReceivedMessageDto
{
    public string MessageId { get; set; }
    public string Sender { get; set; }
    public long Epoch { get; set; }
    public long SourceChainId { get; set; }
    public long TargetChainId { get; set; }
    public string Receiver { get; set; }
    public long TransactionTime { get; set; }
    public string Message { get; set; }
    [CanBeNull] public TokenTransferMetadata TokenAmountInfo { get; set; }
    public HexBigInteger BlockNumber { get; set; }
}