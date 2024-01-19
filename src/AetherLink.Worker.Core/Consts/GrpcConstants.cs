using System.Threading;

namespace AetherLink.Worker.Core.Consts;

public class GrpcConstants
{
    public const int DefaultRequestTimeout = 30;

    public const int DefaultMaxAttempts = 5000;
    public const int DefaultMaxBackoff = 50000;
    public const int DefaultInitialBackoff = 1;
    public const double DefaultBackoffMultiplier = 1.5;

    public const int GracefulShutdown = 5;
}