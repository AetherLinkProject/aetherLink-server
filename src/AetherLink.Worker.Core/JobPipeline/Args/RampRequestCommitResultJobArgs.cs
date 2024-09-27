namespace AetherLink.Worker.Core.JobPipeline.Args;

public class RampRequestCommitResultJobArgs
{
    public string MessageId { get; set; }
    public string ChainId { get; set; }
    public int RoundId { get; set; }
    public long Epoch { get; set; }
    public string TransactionId { get; set; }
}