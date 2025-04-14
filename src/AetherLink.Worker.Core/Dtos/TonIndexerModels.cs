using System;
using System.Collections.Generic;

namespace AetherLink.Worker.Core.Dtos;

public class TransactionsResponse
{
    public List<Transaction> Transactions { get; set; }
}

public class Transaction
{
    public BlockId BlockRef { get; set; }
    public TransactionDescr Description { get; set; }
    public string Hash { get; set; }
    public Message InMsg { get; set; }
    public int McBlockSeqno { get; set; }
    public string Lt { get; set; }
    public int Now { get; set; }
    public List<Message> OutMsgs { get; set; }
    public string PrevTransHash { get; set; }
    public string TraceId { get; set; }

    public CrossChainToTonTransactionDto ConvertToTonTransactionDto()
    {
        string outMsg = null;
        if (OutMsgs != null && OutMsgs.Count > 0)
        {
            outMsg = OutMsgs[0].MessageContent.Body;
        }

        var tx = new CrossChainToTonTransactionDto
        {
            WorkChain = BlockRef.Workchain,
            Shard = BlockRef.Shard,
            SeqNo = BlockRef.Seqno,
            TraceId = TraceId,
            TransactionLt = Lt,
            Hash = Hash,
            Aborted = Description.Aborted,
            PrevHash = PrevTransHash,
            BlockTime = Now,
            Body = InMsg.MessageContent.Body,
            OutMessage = outMsg,
            Success = Description.ComputePh.Success && Description.Action.Success,
            ExitCode = Description.ComputePh.ExitCode,
            Bounce = InMsg.Bounce ?? false,
            Bounced = InMsg.Bounced ?? false,
            OpCode = Convert.ToInt32(InMsg.Opcode, 16)
        };

        return tx;
    }
}

public class BlockId
{
    public Int64 Seqno { get; set; }
    public string Shard { get; set; }
    public int Workchain { get; set; }
}

public class TransactionDescr
{
    public bool Aborted { get; set; }
    public ActionPhase Action { get; set; }
    public ComputePhase ComputePh { get; set; }
}

public class Message
{
    public bool? Bounce { get; set; }
    public bool? Bounced { get; set; }
    public MessageContent MessageContent { get; set; }
    public string Opcode { get; set; }
}

public class MessageContent
{
    public string Body { get; set; }
}

public class ActionPhase
{
    public bool Success { get; set; }
}

public class ComputePhase
{
    public int ExitCode { get; set; }
    public bool Success { get; set; }
}