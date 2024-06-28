namespace AetherLink.MockServer.GraphQL.Dtos;

public class OcrJobEventDto : OracleBasicDto
{
    public int RequestTypeIndex { get; set; }
    public string TransactionId { get; set; }
    public long StartTime { get; set; }
    public string Commitment { get; set; }
}