using System.Collections.Generic;

namespace AetherLink.Worker.Core.Dtos;

public class LatestRoundsDto
{
    public List<LatestRoundDto> LatestRounds { get; set; }
}

public class LatestRoundDto
{
    public long EpochAndRound { get; set; }
}