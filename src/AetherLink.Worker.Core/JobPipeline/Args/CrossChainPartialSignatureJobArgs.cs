using AetherLink.Worker.Core.Dtos;

namespace AetherLink.Worker.Core.JobPipeline.Args;

public class CrossChainPartialSignatureJobArgs
{
    public ReportContextDto ReportContext { get; set; }
}