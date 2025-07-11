using System;
using AetherLink.Worker.Core.Constants;

namespace AetherLink.Worker.Core.Dtos;

public class CrossChainDataDto
{
    public ReportContextDto ReportContext { get; set; }
    public string Message { get; set; }
    public TokenTransferMetadataDto TokenTransferMetadata { get; set; }

    // When the task begins a new round, the RequestReceiveTime will become the starting point of the next round's time window.
    public DateTime RequestReceiveTime { get; set; }
    public CrossChainState State { get; set; }

    // target chain transaction information
    public string ResendTransactionId { get; set; }
    public long ResendTransactionBlockHeight { get; set; }
    public DateTime ResendTransactionBlockTime { get; set; }
    public int NextCommitDelayTime { get; set; }
    public int RequestEndTimeoutWindow { get; set; } = RequestProgressConstants.DefaultRequestEndTimeoutWindow;
}