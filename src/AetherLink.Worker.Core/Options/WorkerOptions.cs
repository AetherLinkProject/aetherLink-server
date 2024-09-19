namespace AetherLink.Worker.Core.Options;

public class WorkerOptions
{
    public int SearchTimer { get; set; } = 100;
    public int UnconfirmedTimer { get; set; } = 100;
    public int PollerTimer { get; set; } = 100;
}