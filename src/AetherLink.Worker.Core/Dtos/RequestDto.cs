using System;

namespace AetherLink.Worker.Core.Dtos;

public class RequestDto : RequestBase
{
    public DateTime RequestReceiveTime { get; set; }
    public DateTime RequestStartTime { get; set; }
    public DateTime ObservationResultCommitTime { get; set; }
    public DateTime ReportSendTime { get; set; }
    public DateTime ReportSignTime { get; set; }
    public DateTime RequestEndTime { get; set; }
    public DateTime RequestCanceledTime { get; set; }
    public RequestState State { get; set; }
    public string TransactionId { get; set; }
    public long TransactionBlockTime { get; set; }
    public bool Retrying { get; set; }
}

public class TimeIntervalWindow
{
    public DateTime Time { get; set; }
    public long Timeout { get; set; }
}