using Google.Protobuf.WellKnownTypes;
using AetherLink.Multisignature;

namespace AetherLink.Worker.Core.JobPipeline.Args;

public class GenerateMultiSignatureJobArgs : JobPipelineArgsBase
{
    public PartialSignatureDto PartialSignature { get; set; }
}