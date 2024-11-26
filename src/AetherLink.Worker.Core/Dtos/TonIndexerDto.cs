using System;

namespace AetherLink.Worker.Core.Dtos;

public class TonIndexerDto
{
    public long BlockHeight { get; set; }
    public string LatestTransactionHash { get; set; }
    public string LatestTransactionLt { get; set; } = "0";
    public int SkipCount { get; set; }
    public long IndexerTime { get; set; }
}

public class TonReceiveEpochInfoDto
{
    public long EpochId { get; set; } = 0;
}

public class CrossChainToTonTransactionDto
{
    public int WorkChain { get; set; }
    public string Shard { get; set; }
    public long SeqNo { get; set; }
    public string TraceId { get; set; }
    public string Hash { get; set; }
    public string PrevHash { get; set; }
    public long BlockTime { get; set; }
    public string TransactionLt { get; set; }
    public int OpCode { get; set; }
    public string OutMessage { get; set; }
    
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