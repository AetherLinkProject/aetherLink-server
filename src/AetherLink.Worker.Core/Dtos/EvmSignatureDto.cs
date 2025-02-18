namespace AetherLink.Worker.Core.Dtos;

public class EvmSignatureDto
{
    public string[] R { get; set; }
    public string[] S { get; set; }
    public string V { get; set; }
}