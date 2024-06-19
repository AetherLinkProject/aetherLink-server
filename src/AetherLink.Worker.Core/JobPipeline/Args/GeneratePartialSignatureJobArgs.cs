using Google.Protobuf;

namespace AetherLink.Worker.Core.JobPipeline.Args;

public class GeneratePartialSignatureJobArgs : JobPipelineArgsBase
{
    // public List<long> Observations { get; set; }
    public ByteString Observations { get; set; }
}