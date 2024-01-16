namespace AetherLink.Worker.Core.Options;

public class SchedulerOptions
{
    public int RetryCount { get; set; } = 3;
    public int CheckRequestReceiveTimeOut { get; set; } = 10;
    public int CheckReportReceiveTimeOut { get; set; } = 10;
    public int CheckObservationResultCommitTimeOut { get; set; } = 5;
    public int CheckReportCommitTimeOut { get; set; } = 5;
    public int CheckTransmitTimeOut { get; set; } = 5;
    public int CheckRequestEndTimeOut { get; set; } = 30;
    public int RetryTimeOut { get; set; } = 1;
}