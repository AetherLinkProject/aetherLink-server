using System.Collections.Generic;
using AetherLink.Worker.Core.Dtos;

namespace AetherLink.Worker.Core.JobPipeline.Args;

public class CrossChainCommitJobArgs
{
    public ReportContextDto ReportContext { get; set; }
    public Dictionary<int, byte[]> PartialSignatures { get; set; }
    public CrossChainDataDto CrossChainData { get; set; }
}