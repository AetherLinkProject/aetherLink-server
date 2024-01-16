namespace AetherLink.Worker.Core.Options;

public class ProcessJobOptions
{
    public int RetryCount { get; set; } = 5;
    public int TransactionResultDefaultDelayTime { get; set; } = 5;
}