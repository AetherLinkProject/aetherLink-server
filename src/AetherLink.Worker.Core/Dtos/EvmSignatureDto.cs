namespace AetherLink.Worker.Core.Dtos;

public class EvmSignatureDto
{
    public byte[][] R { get; set; }
    public byte[][] S { get; set; }
    public byte[] V { get; set; }
}