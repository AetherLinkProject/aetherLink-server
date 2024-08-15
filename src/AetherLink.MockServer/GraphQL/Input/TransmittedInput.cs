namespace AetherLink.MockServer.GraphQL.Input;

public class TransmittedInput
{
    public long FromBlockHeight { get; set; }
    public long ToBlockHeight { get; set; }
    public string ChainId { get; set; }
}