using AetherLink.Worker.Core.Dtos;

namespace AetherLink.Worker.Core.JobPipeline.Args;

public class CrossChainRequestStartArgs
{
    public ReportContextDto ReportContext { get; set; }
    public string Message { get; set; }
    public TokenTransferMetadataDto TokenTransferMetadata { get; set; }
    public long StartTime { get; set; }
}