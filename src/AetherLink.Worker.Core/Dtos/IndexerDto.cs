using System.Collections.Generic;

namespace AetherLink.Worker.Core.Dtos;

public class IndexerLogEventListDto
{
    public List<OcrLogEventDto> OcrJobEvents { get; set; }
}

public class RequestsDto
{
    public List<OcrLogEventDto> Requests { get; set; }
}

public class OcrLogEventDto
{
    public string RequestId { get; set; }
    public int RequestTypeIndex { get; set; }

    public string ChainId { get; set; }
    public string TransactionId { get; set; }
    public long StartTime { get; set; }

    public long Epoch { get; set; }
    // todo switch method name 
}