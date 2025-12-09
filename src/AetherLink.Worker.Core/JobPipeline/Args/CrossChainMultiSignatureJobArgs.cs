using AetherLink.Worker.Core.Dtos;

namespace AetherLink.Worker.Core.JobPipeline.Args;

public class CrossChainMultiSignatureJobArgs
{
    public ReportContextDto ReportContext { get; set; }
    public byte[] Signature { get; set; }
    public int Index { get; set; }
}