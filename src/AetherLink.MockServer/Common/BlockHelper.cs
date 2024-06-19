namespace AetherLink.MockServer.Common;

public static class BlockHelper
{
    public static long GetMockBlockHeight() => new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
}