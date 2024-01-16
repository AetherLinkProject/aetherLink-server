namespace AetherLink.MockServer.GraphQL.Input;

public class SyncStateInput
{
    public string ChainId { get; set; }
    public BlockFilterType FilterType { get; set; }
}

public enum BlockFilterType
{
    BLOCK,
    TRANSACTION,
    LOG_EVENT
}