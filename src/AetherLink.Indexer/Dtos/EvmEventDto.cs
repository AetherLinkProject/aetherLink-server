using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace AetherLink.Indexer.Dtos;

[Event("RequestSent")]
public class SendEventDTO : IEventDTO
{
    [Parameter("bytes32", "messageId", 1, true)]
    public byte[] MessageId { get; set; }

    [Parameter("uint256", "messageId", 2, false)]
    public BigInteger Epoch { get; set; }

    [Parameter("address", "sender", 3, true)]
    public string Sender { get; set; }

    [Parameter("string", "receiver", 4, false)]
    public string Receiver { get; set; }

    [Parameter("uint256", "sourceChainId", 5, false)]
    public BigInteger SourceChainId { get; set; }

    [Parameter("uint256", "targetChainId", 6, false)]
    public BigInteger TargetChainId { get; set; }

    [Parameter("bytes", "message", 7, false)]
    public byte[] Message { get; set; }

    [Parameter("bytes", "tokenTransferMetadataBytes", 8, false)]
    public byte[] TokenTransferMetadataBytes { get; set; }
}

[Event("ForwardMessageCalled")]
public class ForwardMessageCalledEventDTO : IEventDTO
{
    [Parameter("bytes32", "messageId", 1, true)]
    public byte[] MessageId { get; set; }

    [Parameter("uint256", "sourceChainId", 2, false)]
    public BigInteger SourceChainId { get; set; }

    [Parameter("uint256", "targetChainId", 3, false)]
    public BigInteger TargetChainId { get; set; }

    [Parameter("string", "sender", 4, false)]
    public string Sender { get; set; }

    [Parameter("address", "receiver", 5, false)]
    public string Receiver { get; set; }

    [Parameter("bytes", "message", 6, false)]
    public byte[] Message { get; set; }
}