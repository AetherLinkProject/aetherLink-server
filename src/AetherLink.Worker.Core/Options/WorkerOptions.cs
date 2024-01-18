using System.Collections.Generic;

namespace AetherLink.Worker.Core.Options;

public class WorkerOptions
{
    public int SearchTimer { get; set; } = 30;
    public int ReconnectDelay { get; set; } = 10;
    public int LogBackFillBatchSize { get; set; } = 100;
    public List<ChainInfo> Chains { get; set; }
}

public class ChainInfo
{
    public string ChainId { get; set; }
    public long LatestHeight { get; set; } = -1;
}