namespace AetherLink.Worker.Core.Options;

public class LogPollerOptions
{
    public int PollerTimer { get; set; } = 3000;
}

public class UnconfirmedPollerOptions
{
    public int PollerTimer { get; set; } = 30;
    public int UnconfirmedBlockBatchSize { get; set; } = 100;
}