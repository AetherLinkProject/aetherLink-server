namespace AetherLink.Worker.Core.JobPipeline.Args;

public class JobPipelineArgsBase
{
    public string RequestId { get; set; }
    public string ChainId { get; set; }
    public string TransactionId { get; set; }
    public int RoundId { get; set; }
    public long Epoch { get; set; }
    public long StartTime { get; set; }
}