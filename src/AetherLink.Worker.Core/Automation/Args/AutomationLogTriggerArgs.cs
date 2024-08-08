using AetherLink.Worker.Core.OCR;

namespace AetherLink.Worker.Core.Automation.Args;

public class AutomationLogTriggerArgs : OCRBasicDto
{
    // Basic
    // public string ChainId { get; set; }
    // public string UpkeepId { get; set; }

    // Event Info
    // public long BlockHeight { get; set; }
    public long StartTime { get; set; }
    public string TransactionEventStorageId { get; set; }
    public string LogUpkeepStorageId { get; set; }
}