namespace AetherLink.Worker.Core.JobPipeline.Args;

public class RampRequestMultiSignatureJobArgs
{
    public string MessageId { get; set; }
    public string ChainId { get; set; }
    public int RoundId { get; set; }
    public long Epoch { get; set; }
    public byte[] Signature { get; set; }
    public int Index { get; set; }
}