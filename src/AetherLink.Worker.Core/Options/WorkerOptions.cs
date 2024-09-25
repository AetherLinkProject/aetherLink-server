namespace AetherLink.Worker.Core.Options;

public class WorkerOptions
{
    public int SearchTimer { get; set; } = 3000;
    public int UnconfirmedTimer { get; set; } = 1000;
    public int PollerTimer { get; set; } = 3000;
}