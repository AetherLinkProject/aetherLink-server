using AetherLink.Worker.Core.JobPipeline.Args;

namespace AetherLink.Worker.Core.Automation.Args;

public class AutomationJobArgs : JobPipelineArgsBase
{
    // public string Cron { get; set; }
    // public string JobSpec { get; set; }
    public long BlockHeight { get; set; }
    public string JobSpec { get; set; }
}