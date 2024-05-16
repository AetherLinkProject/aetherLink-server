
namespace AetherLink.Worker.Core.JobPipeline.Args;

public class GenerateReportJobArgs : JobPipelineArgsBase
{
    public long Data { get; set; }
    public int Index { get; set; }
}