using System.Collections.Generic;
using Google.Protobuf.WellKnownTypes;

namespace AetherLink.Worker.Core.JobPipeline.Args;

public class FollowerReportProcessJobArgs : JobPipelineArgsBase
{
    public List<long> Observations { get; set; }
    public Timestamp ReportStartSignTime { get; set; }
}