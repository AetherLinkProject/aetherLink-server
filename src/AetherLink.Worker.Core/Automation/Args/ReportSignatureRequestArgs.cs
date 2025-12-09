using AetherLink.Worker.Core.Dtos;

namespace AetherLink.Worker.Core.Automation.Args;

public class ReportSignatureRequestArgs : OCRBasicDto
{
    public byte[] Payload { get; set; }
}