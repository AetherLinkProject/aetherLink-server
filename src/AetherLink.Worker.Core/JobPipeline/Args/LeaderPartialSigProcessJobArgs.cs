using Google.Protobuf.WellKnownTypes;
using AetherLink.Multisignature;

namespace AetherLink.Worker.Core.JobPipeline.Args;

public class LeaderPartialSigProcessJobArgs : JobPipelineArgsBase
{
    public PartialSignatureDto PartialSignature { get; set; }
}