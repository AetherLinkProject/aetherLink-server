using System.Collections.Generic;

namespace AetherLink.Indexer.Dtos;

public class TonIndexerDto
{
    public long BlockHeight { get; set; }
    public string LatestTransactionHash { get; set; }
    public string LatestTransactionLt { get; set; } = "0";
    public int SkipCount { get; set; }
    public long IndexerTime { get; set; }
}

public class TonCenterGetTransactionsResponseDto
{
    public List<TonTransactionDto> Transactions { get; set; }
}

public class TonTransactionDto
{
    public string Hash { get; set; }
    public long Lt { get; set; }
    public string TraceId { get; set; }
    public TonInMessageDto InMsg { get; set; }
    public List<TonInMessageDto> OutMsgs { get; set; }
}

public class TonInMessageDto
{
    public string Opcode { get; set; }
    public TonMessageContentDto MessageContent { get; set; }
}

public class TonMessageContentDto
{
    public string Body { get; set; }
}