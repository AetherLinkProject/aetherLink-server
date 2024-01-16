using AetherLink.Worker.Core.Dtos;

namespace AetherLink.Worker.Core.JobPipeline.Args;

public class DataFeedsProcessJobArgs : JobPipelineArgsBase
{
    public string Cron { get; set; }
    public DataFeedsDto DataFeedsDto { get; set; }
    public string JobSpec { get; set; }
}