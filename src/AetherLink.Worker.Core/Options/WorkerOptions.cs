using System.Collections.Generic;

namespace AetherLink.Worker.Core.Options;

public class WorkerOptions
{
    public int SearchTimer { get; set; } = 30;
    public List<ChainInfo> Chains { get; set; }
    public int UnconfirmedLogBatchSize { get; set; } = 10;
}

public class ChainInfo
{
    public string ChainId { get; set; }
    public long LatestHeight { get; set; } = -1;
}