namespace AetherLink.Worker.Core.Dtos;

public class RequestBase
{
    public string ChainId { get; set; }
    public string RequestId { get; set; }
    public long Epoch { get; set; }
    public string TransactionId { get; set; }

    // roundId only updated in two ways, transmitted => 0 | end time window => round+=1
    public int RoundId { get; set; }
}