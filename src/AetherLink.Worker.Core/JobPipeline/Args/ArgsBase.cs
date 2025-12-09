using AetherLink.Worker.Core.Dtos;

namespace AetherLink.Worker.Core.JobPipeline.Args;

public class JobPipelineArgsBase : OracleRequestBase
{
    public long StartTime { get; set; }
}