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

// Ramp Request event
public class IndexerRampRequestListDto
{
    public List<RampRequestDto> RampRequests { get; set; }
}

public class RampRequestDto
{
    public string ChainId { get; set; }
    public string TransactionId { get; set; }
    public string MessageId { get; set; }
    public long TargetChainId { get; set; }
    public long SourceChainId { get; set; }
    public string Sender { get; set; }
    public string Receiver { get; set; }
    public string Data { get; set; }
    public long Epoch { get; set; }
    public long StartTime { get; set; }
}

// Transmitted event
public class IndexerTransmittedListDto
{
    public List<TransmittedDto> Transmitted { get; set; }
}

public class TransmittedDto : IndexerBasicDto
{
    public string TransactionId { get; set; }
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

public class IndexerTransactionEventListDto
{
    public List<TransactionEventDto> TransactionEvents { get; set; }
}

public class TransactionEventDto
{
    public string ChainId { get; set; }
    public string BlockHash { get; set; }
    public long BlockHeight { get; set; }
    public string TransactionId { get; set; }
    public string MethodName { get; set; }
    public long StartTime { get; set; }
    public string ContractAddress { get; set; }
    public string EventName { get; set; }
    public int Index { get; set; }
}