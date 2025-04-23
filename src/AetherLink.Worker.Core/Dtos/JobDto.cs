using System;
using AetherLink.Worker.Core.Constants;

namespace AetherLink.Worker.Core.Dtos;

public class JobDto : OracleRequestBase
{
    public DateTime RequestReceiveTime { get; set; }
    public RequestState State { get; set; }
    public long TransactionBlockTime { get; set; }
    public string JobSpec { get; set; }
    public int RequestEndTimeoutWindow { get; set; } = RequestProgressConstants.DefaultRequestEndTimeoutWindow;
}