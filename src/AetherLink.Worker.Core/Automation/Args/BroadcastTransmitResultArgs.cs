using AetherLink.Worker.Core.OCR;

namespace AetherLink.Worker.Core.Automation.Args;

public class BroadcastTransmitResultArgs : OCRBasicDto
{
    public string TransactionId { get; set; }
}