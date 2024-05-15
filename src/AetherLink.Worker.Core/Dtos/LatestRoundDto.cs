namespace AetherLink.Worker.Core.Dtos;

public class OracleLatestEpochRecord
{
    public OracleLatestEpochDto OracleLatestEpoch { get; set; }
}

public class OracleLatestEpochDto
{
    public string ChainId { get; set; }
    public long Epoch { get; set; }
}