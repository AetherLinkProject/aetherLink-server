namespace AetherLink.Multisignature;

public struct PartialSignatureDto
{
    public byte[] Signature { get; set; }
    public int Index { get; set; }
}