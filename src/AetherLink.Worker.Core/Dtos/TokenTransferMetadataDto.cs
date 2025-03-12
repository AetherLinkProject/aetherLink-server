namespace AetherLink.Worker.Core.Dtos;

public class TokenTransferMetadataDto
{
    public long TargetChainId { get; set; }
    public string TokenAddress { get; set; }
    public string Symbol { get; set; }
    public long Amount { get; set; }
    public string ExtraData { get; set; }
}