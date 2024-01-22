namespace AetherLink.Worker.Core.Options;

public class SchedulerOptions
{
    public int CheckRequestEndTimeoutWindow { get; set; } = 10;
    public int ObservationCollectTimeoutWindow { get; set; } = 5;
    public int RetryTimeOut { get; set; } = 1;
}