using AetherLink.Worker.Core.OCR;

namespace AetherLink.Worker.Core.Automation.Args;

public class AutomationLogTriggerArgs : OCRBasicDto
{
    public long StartTime { get; set; }
    public string TransactionEventStorageId { get; set; }
    public string LogUpkeepStorageId { get; set; }
}