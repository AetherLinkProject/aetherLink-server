namespace AetherLink.MockServer.GraphQL.Dtos;

public class OracleLatestEpochDto
{
    public string ChainId { get; set; }
    public long EpochAndRound { get; set; }
}