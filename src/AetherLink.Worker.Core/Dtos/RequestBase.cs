namespace AetherLink.Worker.Core.Dtos;

public class RequestBase
{
    public string ChainId { get; set; }
    public string RequestId { get; set; }
    public string JobSpec { get; set; }
    public int RoundId { get; set; }
    public long Epoch { get; set; }
}