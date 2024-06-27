using AetherLink.Contracts.Automation;

namespace AetherLink.Worker.Core.Dtos;

public class AutomationTriggerDataDto
{
    public string Cron { get; set; }
    public TriggerDataSpecDto TriggerDataSpec { get; set; }
}

public class TriggerDataSpecDto
{
    public TriggerType TriggerType { get; set; }
}