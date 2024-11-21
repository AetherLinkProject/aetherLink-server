namespace AetherLink.Server.Grains;

public static class TimeHelper
{
    public static long GetTimeStampInMilliseconds() => new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
}