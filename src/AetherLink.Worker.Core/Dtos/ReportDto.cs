using System.Collections.Generic;

namespace AetherLink.Worker.Core.Dtos;

public class ReportDto : RequestBase
{
    public List<long> Observations { get; set; }
}