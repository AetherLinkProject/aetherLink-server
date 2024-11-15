using AetherLink.Worker.Core.Dtos;

namespace AetherLink.Worker.Core.JobPipeline.Args;

public class CrossChainReceivedResultCheckJobArgs
{
    public ReportContextDto ReportContext { get; set; }
    public string CommitTransactionId { get; set; }
}