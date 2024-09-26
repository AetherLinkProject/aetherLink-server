using System;

namespace AetherLink.Worker.Core.Dtos;

public class RampMessageDto
{
    public string ChainId { get; set; }
    public string TransactionId { get; set; }
    public string MessageId { get; set; }
    public long TargetChainId { get; set; }
    public long SourceChainId { get; set; }
    public string Sender { get; set; }
    public string Receiver { get; set; }
    public string Data { get; set; }
    
    // request metadata
    public long Epoch { get; set; }
    public int Round { get; set; }

    // When the task begins a new round, the RequestReceiveTime will become the starting point of the next round's time window.
    public DateTime RequestReceiveTime { get; set; }
    public RampRequestState State { get; set; }

    // target chain transaction information
    public string ResendTransactionId { get; set; }
    public long ResendTransactionBlockHeight { get; set; }
    public string NextCommitDelayTime { get; set; }
}