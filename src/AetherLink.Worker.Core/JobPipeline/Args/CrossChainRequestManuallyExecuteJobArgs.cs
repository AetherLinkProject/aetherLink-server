namespace AetherLink.Worker.Core.JobPipeline.Args;

public class CrossChainRequestManuallyExecuteJobArgs
{
    public string MessageId { get; set; }
    public long StartTime { get; set; }
}