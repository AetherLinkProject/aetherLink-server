namespace AetherLink.Worker.Core.Dtos;

public class VrfJobDto
{
    public string ChainId { get; set; }
    public string RequestId { get; set; }
    public string TransactionId { get; set; }
    public VrfJobState Status { get; set; }
}

public enum VrfJobState
{
    CheckPending = 1,
    Retrying,
    Consumed
}