using Google.Protobuf.WellKnownTypes;

namespace AetherLink.MockServer.GraphQL.Dtos;

public class OcrJobEventDto
{
    public string ChainId { get; set; }
    public string RequestId { get; set; }
    public int RequestTypeIndex { get; set; }
    public string TransactionId { get; set; }
    public long StartTime { get; set; }
    public long Epoch { get; set; }
}