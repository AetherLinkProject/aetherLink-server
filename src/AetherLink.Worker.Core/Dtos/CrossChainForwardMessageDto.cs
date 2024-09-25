using System;
using System.Collections.Generic;

namespace AetherLink.Worker.Core.Dtos;

public class CrossChainForwardMessageDto
{
    /// <summary>
    /// base64 encode
    /// </summary>
    public string MessageId { get; set; }
    
    public Int64 SourceChainId { get; set; }
    
    public Int64 TargetChainId { get; set; }
    
    /// <summary>
    /// base64 encode
    /// </summary>
    public string TargetContractAddress { get; set; }
    
    /// <summary>
    /// base64 encode
    /// </summary>
    public string Sender { get; set; }
    
    /// <summary>
    /// base64 encode
    /// </summary>
    public string Receiver { get; set; }
    
    /// <summary>
    /// base64 encode
    /// </summary>
    public string Message { get; set; }
}

public class TransactionAnalysisDto<TX, TLatestTx>
{ 
    public List<TX> ResendTxList { get; set; }
    public List<TX> ForwardTxList { get; set; }
    public List<TX> ReceiveTxList { get; set; }
    public TLatestTx LatestTransactions { get; set; }
}

public class CrossChainForwardResendDto
{
    public string MessageId { get; set; }
    
    public Int64 TargetBlockHeight { get; set; }
    
    public string Hash { get; set; }
    
    public Int64 TargetBlockGeneratorTime { get; set; }
    
    public Int64 CheckCommitTime { get; set; }
    
    public ResendStatus Status { get; set; }
}

public enum ResendStatus
{
    WaitConsensus = 1,
    WaitCommit = 2,
    ChainConfirm = 3,
}