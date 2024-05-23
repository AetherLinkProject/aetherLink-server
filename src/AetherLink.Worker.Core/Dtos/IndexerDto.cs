using System.Collections.Generic;

namespace AetherLink.Worker.Core.Dtos;

// Job event
public class IndexerLogEventListDto
{
    public List<OcrLogEventDto> OcrJobEvents { get; set; }
}

public class OcrLogEventDto : IndexerBasicDto
{
    public int RequestTypeIndex { get; set; }

    public string TransactionId { get; set; }
    public long StartTime { get; set; }
}

// Transmitted event
public class IndexerTransmittedListDto
{
    public List<TransmittedDto> Transmitted { get; set; }
}

public class TransmittedDto : IndexerBasicDto
{
    public long Epoch { get; set; }
    public long StartTime { get; set; }
}

// Cancelled event
public class IndexerRequestCancelledListDto
{
    public List<RequestCancelledDto> RequestCancelled { get; set; }
}

public class RequestCancelledDto : IndexerBasicDto
{
}

// Basic Dto
public class IndexerBasicDto
{
    public string ChainId { get; set; }
    public string RequestId { get; set; }
    public long BlockHeight { get; set; }
    public string BlockHash { get; set; }
}