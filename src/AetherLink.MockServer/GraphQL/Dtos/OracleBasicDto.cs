namespace AetherLink.MockServer.GraphQL.Dtos;

public class OracleBasicDto
{
    public string ChainId { get; set; }
    public string RequestId { get; set; }
    public long BlockHeight { get; set; }
    public string BlockHash { get; set; }
}