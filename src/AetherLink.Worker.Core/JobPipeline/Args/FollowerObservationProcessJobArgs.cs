using Google.Protobuf.WellKnownTypes;

namespace AetherLink.Worker.Core.JobPipeline.Args;

public class FollowerObservationProcessJobArgs : JobPipelineArgsBase
{
    public Timestamp RequestStartTime { get; set; }
}