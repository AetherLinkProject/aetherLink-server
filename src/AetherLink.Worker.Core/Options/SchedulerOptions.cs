namespace AetherLink.Worker.Core.Options;

public class SchedulerOptions
{
    public int CheckRequestEndTimeoutWindow { get; set; } = 5;
    public int ObservationCollectTimeoutWindow { get; set; } = 3;
    public int RetryTimeOut { get; set; } = 1;
    public double CheckCommittedTimeoutWindow { get; set; } = 5;
}