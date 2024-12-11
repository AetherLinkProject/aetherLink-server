namespace AetherLink.Worker.Core.Constants;

public static class RetryConstants
{
    // The number of retries multiplied by the interval should be less than the timeout window.
    public const int DefaultDelay = 10;
    public const int CheckResultDelay = 3;
}