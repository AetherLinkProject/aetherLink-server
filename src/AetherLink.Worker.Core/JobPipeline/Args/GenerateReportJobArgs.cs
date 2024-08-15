
namespace AetherLink.Worker.Core.JobPipeline.Args;

public class GenerateReportJobArgs : JobPipelineArgsBase
{
    public string Data { get; set; }
    public int Index { get; set; }
}