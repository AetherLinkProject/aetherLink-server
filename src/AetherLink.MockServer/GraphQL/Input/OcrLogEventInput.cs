namespace AetherLink.MockServer.GraphQL.Input;

public class OcrLogEventInput
{
    public long FromBlockHeight { get; set; }
    public long ToBlockHeight { get; set; }
    public string ChainId { get; set; }
}