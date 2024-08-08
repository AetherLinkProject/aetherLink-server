using AetherLink.Worker.Core.JobPipeline.Args;

namespace AetherLink.Worker.Core.Automation.Args;

public class AutomationStartJobArgs : JobPipelineArgsBase
{
    public string JobSpec { get; set; }
}