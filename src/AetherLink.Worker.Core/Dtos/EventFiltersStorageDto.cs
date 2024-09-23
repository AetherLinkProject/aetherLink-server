using System.Collections.Generic;

namespace AetherLink.Worker.Core.Dtos;

public class EventFiltersStorageDto
{
    // ContractAddress-EventName | List<LogUpkeepStorageId>
    public Dictionary<string, List<string>> Filters { get; set; }
}