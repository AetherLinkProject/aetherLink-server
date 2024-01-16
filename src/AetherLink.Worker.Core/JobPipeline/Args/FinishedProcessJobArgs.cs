namespace AetherLink.Worker.Core.JobPipeline.Args;

public class FinishedProcessJobArgs : JobPipelineArgsBase
{
    public string TransactionId { get; set; }
}