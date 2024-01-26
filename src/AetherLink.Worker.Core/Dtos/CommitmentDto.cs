namespace AetherLink.Worker.Core.Dtos;

public class RequestCommitmentRecord
{
    public CommitmentDto RequestCommitment { get; set; }
}

public class CommitmentDto
{
    public string ChainId { get; set; }
    public string RequestId { get; set; }
    public string Commitment { get; set; }
}