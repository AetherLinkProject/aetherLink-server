using AetherLink.Worker.Core.Dtos;

namespace AetherLink.Worker.Core.JobPipeline.Args;

public class RequestStartProcessJobArgs : JobPipelineArgsBase
{
    public DataFeedsDto DataFeedsDto { get; set; }
    public string JobSpec { get; set; }
}