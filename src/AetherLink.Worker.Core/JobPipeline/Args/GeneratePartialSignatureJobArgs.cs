using System.Collections.Generic;
using Google.Protobuf.WellKnownTypes;

namespace AetherLink.Worker.Core.JobPipeline.Args;

public class GeneratePartialSignatureJobArgs : JobPipelineArgsBase
{
    public List<long> Observations { get; set; }
}