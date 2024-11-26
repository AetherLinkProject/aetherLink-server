namespace AetherLink.Worker.Core.Dtos;

public class TokenAmountDto
{
    public string SwapId { get; set; }
    public long TargetChainId { get; set; }
    public string TargetContractAddress { get; set; }
    public string TokenAddress { get; set; }
    public string OriginToken { get; set; }
    public long Amount { get; set; }
}