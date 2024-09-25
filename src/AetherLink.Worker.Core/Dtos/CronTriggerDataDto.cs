namespace AetherLink.Worker.Core.Dtos;

public class CronTriggerDataDto
{
    public string Cron { get; set; }
}

public class LogTriggerDataSpecDto
{
    public string ChainId { get; set; }
    public string ContractAddress { get; set; }
    public string EventName { get; set; }
}