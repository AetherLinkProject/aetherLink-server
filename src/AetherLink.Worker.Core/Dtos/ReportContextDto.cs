namespace AetherLink.Worker.Core.Dtos;

public class ReportContextDto
{
    public string MessageId { get; set; }
    public string Sender { get; set; }
    public string Receiver { get; set; }
    public long TargetChainId { get; set; }
    public long SourceChainId { get; set; }
    public long Epoch { get; set; }
    public int RoundId { get; set; }
    public string TargetChainOracleContractAddress { get; set; } = "EQChOx2UsaWRVb8k9NAARNKaNqHVC-uQ98Ff3Uy-qDgqBtMg";
    public long TransactionReceivedTime { get; set; }
}