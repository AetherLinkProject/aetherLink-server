
namespace AetherLink.Worker.Core.JobPipeline.Args;

public class LeaderGenerateReportJobArgs : JobPipelineArgsBase
{
    public long Data { get; set; }
    public int Index { get; set; }
}