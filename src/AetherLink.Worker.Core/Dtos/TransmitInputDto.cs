using System.Collections.Generic;

namespace AetherLink.Worker.Core.Dtos;

public class TransmitInputDto
{
    public List<string> ReportContext { get; set; }
    public string Report { get; set; }
    public List<string> Signature { get; set; }
}