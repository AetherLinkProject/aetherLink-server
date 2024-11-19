namespace AetherLink.Server.HttpApi.Dtos;

public class GetCrossChainRequestStatusInput
{
    public long SourceChainId { get; set; }
    public long TargetChainId { get; set; }
    public string TransactionId { get; set; }
    public string MessageId { get; set; }
}