using System.Collections.Generic;

namespace AetherLink.Worker.Core.Dtos;

public class CommitmentsDto
{
    public List<CommitmentDto> Commitments { get; set; }
}

public class CommitmentDto
{
    public string ChainId { get; set; }
    public string RequestId { get; set; }
    public string Commitment { get; set; }
}