namespace AetherLink.Worker.Core.Dtos;

public class DataMessageDto : OracleRequestBase
{
    public long Data { get; set; }
}

public class AuthFeedDataDto
{
    public string ChainId { get; set; }
    public string RequestId { get; set; }
    public string TransactionId { get; set; }
    public string OldData { get; set; }
    public string NewData { get; set; }
}