using System.Collections.Generic;
using JetBrains.Annotations;

namespace AetherLink.Indexer.Dtos;

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
    public long Epoch { get; set; }
    public long StartTime { get; set; }
    public string Message { get; set; }
    [CanBeNull] public IndexerTokenTransferMetadataDto TokenTransferMetadata { get; set; }
}

public class IndexerTokenTransferMetadataDto
{
    public long? TargetChainId { get; set; }
    [CanBeNull] public string Symbol { get; set; }
    public long? Amount { get; set; }
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

public class IndexerRequestCommitListDto
{
    public List<RampCommitReportAcceptedDto> RampCommitReport { get; set; }
}

public class RampCommitReportAcceptedDto
{
    public long SourceChainId { get; set; }
    public long TargetChainId { get; set; }
    public string TransactionId { get; set; }
    public string MessageId { get; set; }
    public long CommitTime { get; set; }
}

// Cancelled event
public class IndexerRequestCancelledListDto
{
    public List<RequestCancelledDto> RequestCancelled { get; set; }
}

public class RequestCancelledDto : IndexerBasicDto
{
}

public class IndexerRampRequestCancelledListDto
{
    public List<RampRequestCancelledDto> RampRequestCancelled { get; set; }
}

public class RampRequestCancelledDto
{
    public string MessageId { get; set; }
}

public class IndexerRampRequestManuallyExecutedListDto
{
    public List<RampRequestManuallyExecutedDto> RampRequestManuallyExecuted { get; set; }
}

public class RampRequestManuallyExecutedDto
{
    public string MessageId { get; set; }
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

public class RequestCommitmentRecord
{
    public CommitmentDto RequestCommitment { get; set; }
}

public class CommitmentDto
{
    public string ChainId { get; set; }
    public string RequestId { get; set; }
    public string Commitment { get; set; }
}

public class OracleLatestEpochRecord
{
    public OracleLatestEpochDto OracleLatestEpoch { get; set; }
}

public class OracleLatestEpochDto
{
    public string ChainId { get; set; }
    public long Epoch { get; set; }
}

public class OracleConfigDigestRecord
{
    public ConfigDigestDto OracleConfigDigest { get; set; }
}

public class ConfigDigestDto
{
    public string ConfigDigest { get; set; }
}

public class IndexerTokenSwapConfigInfo
{
    public TokenSwapConfigDto TokenSwapConfig { get; set; }
}

public class TokenSwapConfigDto
{
    public string ExtraData { get; set; }
    public long TargetChainId { get; set; }
    public long SourceChainId { get; set; }
    public string Receiver { get; set; }
    public string TokenAddress { get; set; }
    public string Symbol { get; set; }
}