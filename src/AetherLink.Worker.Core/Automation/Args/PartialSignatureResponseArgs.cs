using AetherLink.Worker.Core.Dtos;

namespace AetherLink.Worker.Core.Automation.Args;

public class PartialSignatureResponseArgs : OCRBasicDto
{
    public byte[] Signature { get; set; }
    public int Index { get; set; }
    public byte[] Payload { get; set; }
}