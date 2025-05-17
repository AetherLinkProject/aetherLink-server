namespace AetherLink.Worker.Core.Options;

public class CheckerOptions
{
    public int ProcessDelay { get; set; } = 500;
    public bool EnableJobChecker { get; set; } = true;
}