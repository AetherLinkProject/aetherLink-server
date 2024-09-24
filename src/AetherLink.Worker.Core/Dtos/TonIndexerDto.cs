using System;

namespace AetherLink.Worker.Core.Dtos;

public class TonIndexerDto
{
    public Int64 BlockHeight;

    public string LatestTransactionHash;

    public string LatestTransactionLt;

    public int SkipCount;
}


public class CrossChainToTonTransactionDto
{
    public int WorkChain { get; set; }
    public string Shard { get; set; }
    public Int64 SeqNo { get; set; }
    public string TraceId { get; set; }
    public string Hash { get; set; }
    public string PrevHash { get; set; }
    public Int64 BlockTime { get; set; }
    public int OpCode { get; set; }
    /// <summary>
    /// base64 encode
    /// </summary>
    public string Body { get; set; }
    
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public bool Aborted { get; set; }
    public bool Bounce { get; set; }
    public bool Bounced { get; set; }
}
