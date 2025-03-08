namespace AetherLink.Worker.Core.Dtos;

public class TokenAmountDto
{
    public string ExtraData { get; set; }
    public long TargetChainId { get; set; }
    public string Receiver { get; set; }
    public string TokenAddress { get; set; }
    public string Symbol { get; set; }
    public long Amount { get; set; }
}