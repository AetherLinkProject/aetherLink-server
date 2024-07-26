namespace AetherLink.Worker.Core.Dtos;

public class LogUpkeepInfoDto
{
    public string ChainId { get; set; }
    public string UpkeepId { get; set; }
    public string UpkeepAddress { get; set; }
    public string TriggerChainId { get; set; }
    public string TriggerContractAddress { get; set; }
    public string TriggerEventName { get; set; }
}