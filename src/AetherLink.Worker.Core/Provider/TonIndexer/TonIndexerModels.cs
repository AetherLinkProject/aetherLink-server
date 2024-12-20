using System;
using System.Collections.Generic;
using AetherLink.Worker.Core.Dtos;

namespace AetherLink.Worker.Core.Provider.TonIndexer;

public class TransactionsResponse
{
    public List<Transaction> Transactions { get; set; }
}

public class Transaction
{
    public string Account { get; set; }
    public AccountState AccountStateAfter { get; set; }
    public AccountState AccountStateBefore { get; set; }
    public BlockId BlockRef { get; set; }
    public TransactionDescr Description { get; set; }
    public string EndStatus { get; set; }
    public string Hash { get; set; }
    public Message InMsg { get; set; }
    public int McBlockSeqno { get; set; }
    public string Lt { get; set; }
    public int Now { get; set; }
    public string OrigStatus { get; set; }
    public List<Message> OutMsgs { get; set; }
    public string PrevTransHash { get; set; }
    public string PrevTransLt { get; set; }
    public string TotalFees { get; set; }
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

public class AccountState
{
    public string AccountStatus { get; set; }
    public string Balance { get; set; }
    public string CodeBoc { get; set; }
    public string CodeHash { get; set; }
    public string DataBoc { get; set; }
    public string DataHash { get; set; }
    public string FrozenHash { get; set; }
    public string Hash { get; set; }
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
    public BouncePhase Bounce { get; set; }
    public ComputePhase ComputePh { get; set; }
    public bool CreditFirst { get; set; }
    public CreditPhase CreditPh { get; set; }
    public bool Destroyed { get; set; }
    public bool Installed { get; set; }
    public bool IsTock { get; set; }
    public SplitInfo SplitInfo { get; set; }
    public StoragePhase StoragePh { get; set; }
    public string Type { get; set; }
}

public class Message
{
    public bool? Bounce { get; set; }
    public bool? Bounced { get; set; }
    public string CreatedAt { get; set; }
    public string CreatedLt { get; set; }
    public string Destination { get; set; }
    public string FwdFee { get; set; }
    public string Hash { get; set; }
    public bool? IhrDisabled { get; set; }
    public string IhrFee { get; set; }
    public string ImportFee { get; set; }
    public MessageContent InitState { get; set; }
    public MessageContent MessageContent { get; set; }
    public string Opcode { get; set; }
    public string Source { get; set; }
    public string Value { get; set; }
}

public class MessageContent
{
    public string Body { get; set; }
    public DecodedContent Decoded { get; set; }
    public string Hash { get; set; }
}

public class DecodedContent
{
    public string Comment { get; set; }
    public string Type { get; set; }
}

public class ActionPhase
{
    public string ActionListHash { get; set; }
    public int MsgsCreated { get; set; }
    public bool NoFunds { get; set; }
    public int ResultArg { get; set; }
    public int ResultCode { get; set; }
    public int SkippedActions { get; set; }
    public int SpecActions { get; set; }
    public string StatusChange { get; set; }
    public bool Success { get; set; }
    public int TotActions { get; set; }
    public MessageSize TotMsgSize { get; set; }
    public string TotalActionFees { get; set; }
    public string TotalFwdFees { get; set; }
    public bool Valid { get; set; }
}

public class BouncePhase
{
    public string FwdFees { get; set; }
    public string MsgFees { get; set; }
    public MessageSize MsgSize { get; set; }
    public string ReqFwdFees { get; set; }
    public string Type { get; set; }
}

public class ComputePhase
{
    public bool AccountActivated { get; set; }
    public int ExitArg { get; set; }
    public int ExitCode { get; set; }
    public string GasCredit { get; set; }
    public string GasFee { get; set; }
    public string GasLimit { get; set; }
    public string GasUsed { get; set; }
    public int Mode { get; set; }
    public bool MsgStateUsed { get; set; }
    public string Reason { get; set; }
    public bool Skipped { get; set; }
    public bool Success { get; set; }
    public string VmFinalStateHash { get; set; }
    public string VmInitStateHash { get; set; }
    public int VmSteps { get; set; }
}

public class CreditPhase
{
    public string Credit { get; set; }
    public string DueFeesCollected { get; set; }
}

public class SplitInfo
{
    public int AccSplitDepth { get; set; }
    public int CurShardPfxLen { get; set; }
    public string SiblingAddr { get; set; }
    public string ThisAddr { get; set; }
}

public class StoragePhase
{
    public string StatusChange { get; set; }
    public string StorageFeesCollected { get; set; }
    public string StorageFeesDue { get; set; }
}

public class MessageSize
{
    public string Bits { get; set; }
    public string Cell { get; set; }
}

public class TonBlocks
{
    public List<TonBlockInfo> Blocks { get; set; }
}

public class TonBlockInfo
{
    public bool AfterMerge { get; set; }
    public bool AfterSplit { get; set; }
    public bool BeforeSplit { get; set; }
    public string CreatedBy { get; set; }
    public string EndLt { get; set; }
    public string FileHash { get; set; }
    public int Flags { get; set; }
    public Int64 GenCatchainSeqno { get; set; }
    public string GenUtime { get; set; }
    public Int64 GlobalId { get; set; }
    public bool KeyBlock { get; set; }
    public Int64 MasterRefSeqno { get; set; }
    public TonBlockSummaryInfo BlockSummaryInfoRef { get; set; }
    public Int64 MinRefMcSeqno { get; set; }
    public List<TonBlockSummaryInfo> PrevBlocks { get; set; }
    public Int64 PrevKeyBlockSeqno { get; set; }
    public string RandSeed { get; set; }
    public string RootHash { get; set; }
    public Int64 Seqno { get; set; }
    public string Shard { get; set; }
    public string StartLt { get; set; }
    public int TxCount { get; set; }
    public int ValidatorListHashShort { get; set; }
    public int Version { get; set; }
    public int VertSeqno { get; set; }
    public bool VertSeqnoIncr { get; set; }
    public bool WantMerge { get; set; }
    public bool WantSplit { get; set; }
    public int Workchain { get; set; }
}

public class TonBlockSummaryInfo
{
    public Int64 Seqno { get; set; }
    public string Shard { get; set; }
    public int Workchain { get; set; }
}