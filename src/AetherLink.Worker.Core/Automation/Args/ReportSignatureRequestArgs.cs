using AetherLink.Worker.Core.OCR;

namespace AetherLink.Worker.Core.Automation.Args;

public class ReportSignatureRequestArgs : OCRBasicDto
{
    public byte[] Payload { get; set; }
}