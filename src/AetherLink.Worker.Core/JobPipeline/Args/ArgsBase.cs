using AetherLink.Worker.Core.Dtos;

namespace AetherLink.Worker.Core.JobPipeline.Args;

public class JobPipelineArgsBase : RequestBase
{
    public long StartTime { get; set; }
}