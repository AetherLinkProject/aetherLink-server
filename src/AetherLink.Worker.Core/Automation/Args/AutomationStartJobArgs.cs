using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;

namespace AetherLink.Worker.Core.Automation.Args;

public class AutomationStartJobArgs : JobPipelineArgsBase
{
    public DataFeedsDto DataFeedsDto { get; set; }
    public string JobSpec { get; set; }
}