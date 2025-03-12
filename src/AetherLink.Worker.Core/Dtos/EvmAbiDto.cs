using Nethereum.ABI.FunctionEncoding.Attributes;

namespace AetherLink.Worker.Core.Dtos;

public class ReportContext
{
    [Parameter("bytes32", 1)] public byte[] MessageId { get; set; }
    [Parameter("uint256", 2)] public int SourceChainId { get; set; }
    [Parameter("uint256", 3)] public int TargetChainId { get; set; }
    [Parameter("string", 4)] public string Sender { get; set; }
    [Parameter("address", 5)] public string Receiver { get; set; }
}

public class TokenTransferMetadata
{
    [Parameter("uint256", 1)] public int TargetChainId { get; set; }
    [Parameter("string", 2)] public string TokenAddress { get; set; }
    [Parameter("string", 3)] public string Symbol { get; set; }
    [Parameter("uint256", 4)] public long Amount { get; set; }
    [Parameter("bytes", 5)] public string ExtraData { get; set; }
}