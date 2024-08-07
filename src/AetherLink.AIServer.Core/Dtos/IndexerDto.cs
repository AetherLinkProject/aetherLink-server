using System.Collections.Generic;

namespace AetherLink.AIServer.Core.Dtos;

// Job event
public class AiRequestsListDto
{
    public List<AIRequestDto> AiRequests { get; set; }
}

public class AIRequestDto : IndexerBasicDto
{
    public string TransactionId { get; set; }
    public long StartTime { get; set; }
    public string Commitment { get; set; }
}

// Transmitted event
public class AiReportTransmittedAsyncListDto
{
    public List<AIReportTransmittedDto> AiReportTransmitted { get; set; }
}

public class AIReportTransmittedDto : IndexerBasicDto
{
    public string TransactionId { get; set; }
    public long StartTime { get; set; }
}

// Basic Dto
public class IndexerBasicDto
{
    public string ChainId { get; set; }
    public string RequestId { get; set; }
    public long BlockHeight { get; set; }
    public string BlockHash { get; set; }
}

public class ConfirmedBlockHeightRecord
{
    public SyncState SyncState { get; set; }
}

public class SyncState
{
    public long ConfirmedBlockHeight { get; set; }
}

public enum BlockFilterType
{
    BLOCK,
    TRANSACTION,
    LOG_EVENT
}