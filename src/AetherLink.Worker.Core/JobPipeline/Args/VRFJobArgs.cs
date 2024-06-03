namespace AetherLink.Worker.Core.JobPipeline.Args;

public class VRFJobArgs
{
    public string ChainId { get; set; }
    public string RequestId { get; set; }
    public string TransactionId { get; set; }
    public long BlockHeight { get; set; }
    public string BlockHash { get; set; }
}