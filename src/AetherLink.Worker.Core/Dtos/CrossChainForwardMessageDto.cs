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

public class CrossChainForwardResendStorageDto
{
    public string MessageId;
    public Int64 TargetBlockHeight;
    public string Hash;
    public Int64 TargetBlockGeneratorTime;
    public Int64 CheckCommitTime;
    public ResendStatus Status;
}

public enum ResendStatus
{
    WaitConsensus = 1,
    WaitCommit = 2,
    ChainConfirm = 3,
}
