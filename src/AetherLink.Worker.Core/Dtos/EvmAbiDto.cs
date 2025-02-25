using Nethereum.ABI.FunctionEncoding.Attributes;
using Org.BouncyCastle.Math;

namespace AetherLink.Worker.Core.Dtos;

public class ReportContext
{
    [Parameter("bytes32", 1)] public byte[] MessageId { get; set; }

    // [Parameter("string", 1)] public string MessageId { get; set; }
    [Parameter("uint256", 2)] public int SourceChainId { get; set; }
    [Parameter("uint256", 3)] public int TargetChainId { get; set; }
    [Parameter("string", 4)] public string Sender { get; set; }
    [Parameter("address", 5)] public string Receiver { get; set; }
}

public class TokenAmount
{
    [Parameter("string", 1)] public string SwapId { get; set; }
    [Parameter("uint256", 2)] public int TargetChainId { get; set; }
    [Parameter("string", 3)] public string TargetContractAddress { get; set; }
    [Parameter("string", 4)] public string TokenAddress { get; set; }
    [Parameter("string", 5)] public string OriginToken { get; set; }
    [Parameter("uint256", 6)] public long Amount { get; set; }
}