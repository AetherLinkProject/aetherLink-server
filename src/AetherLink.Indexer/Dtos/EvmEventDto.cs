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

    [Parameter("string", "targetContractAddress", 8, false)]
    public string TargetContractAddress { get; set; }

    [Parameter("string", "tokenAddress", 9, false)]
    public string TokenAddress { get; set; }

    [Parameter("uint256", "amount", 10, false)]
    public BigInteger Amount { get; set; }
}