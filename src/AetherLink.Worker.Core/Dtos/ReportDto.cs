using System.Collections.Generic;

namespace AetherLink.Worker.Core.Dtos;

public class ReportDto : OracleRequestBase
{
    public List<long> Observations { get; set; }
}