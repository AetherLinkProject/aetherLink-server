namespace AetherLink.Worker.Core.JobPipeline.Args;

public class CrossChainCommitAcceptedJobArgs
{
    public long SourceChainId { get; set; }
    public long TargetChainId { get; set; }
    public string TransactionId { get; set; }
    public string MessageId { get; set; }
}