namespace AetherLink.Server.HttpApi.Dtos;

public class GetCrossChainRequestStatusInput
{
    public string TransactionId { get; set; }
    public string TraceId { get; set; }
    // public string MessageId { get; set; }
}