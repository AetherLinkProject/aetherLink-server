using System.Collections.Generic;

namespace AetherLink.Worker.Core.Options;

public class WorkerOptions
{
    public int SearchTimer { get; set; } = 30;
    public int HealthCheckTimer { get; set; } = 5;
    public int HealthCheckMaxRetryTimes { get; set; } = 15;
    public int LogBackFillBatchSize { get; set; } = 10;
    public int BlockBackFillDepth { get; set; } = 500000;
    public List<ChainInfo> Chains { get; set; }
}

public class ChainInfo
{
    public string ChainId { get; set; }
    public long LatestHeight { get; set; } = -1;
}