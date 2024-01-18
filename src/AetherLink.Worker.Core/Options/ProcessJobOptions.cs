namespace AetherLink.Worker.Core.Options;

public class ProcessJobOptions
{
    public int RetryCount { get; set; } = 5;
    public int TransactionResultDelay { get; set; } = 5;
}