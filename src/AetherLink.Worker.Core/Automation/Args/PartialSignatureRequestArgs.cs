using AetherLink.Worker.Core.OCR;

namespace AetherLink.Worker.Core.Automation.Args;

public class PartialSignatureRequestArgs : OCRBasicDto
{
    public byte[] Signature { get; set; }
    public int Index { get; set; }
}