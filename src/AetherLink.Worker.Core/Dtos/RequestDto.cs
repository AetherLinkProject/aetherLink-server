using System;

namespace AetherLink.Worker.Core.Dtos;

public class RequestDto : RequestBase
{
    public DateTime RequestReceiveTime { get; set; }
    public RequestState State { get; set; }
    public long TransactionBlockTime { get; set; }
    public string JobSpec { get; set; }
}