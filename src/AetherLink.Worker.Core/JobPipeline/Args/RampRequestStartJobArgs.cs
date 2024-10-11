namespace AetherLink.Worker.Core.JobPipeline.Args;

public class RampRequestStartJobArgs
{
    public string TransactionId { get; set; }
    public long BlockHeight { get; set; }
    public string MessageId { get; set; }
    public long TargetChainId { get; set; }
    public long SourceChainId { get; set; }
    public string Sender { get; set; }
    public string Receiver { get; set; }
    public string Data { get; set; }
    public long Epoch { get; set; }
    public int RoundId { get; set; }
    public long StartTime { get; set; }
}