namespace AetherLink.Worker.Core.JobPipeline.Args;

public class RampRequestPartialSignatureJobArgs
{
    public string MessageId { get; set; }
    public string ChainId { get; set; }
    public int RoundId { get; set; }
    public long Epoch { get; set; }
}