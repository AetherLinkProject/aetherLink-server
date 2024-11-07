namespace AetherLink.Worker.Core.Constants;

public class TonEnvConstants
{
    public const long ResendMaxWaitSeconds = 30;
    public const int PullTransactionMinWaitSecond = 3;
}

public class TonOpCodeConstants
{
    public const int ForwardTx = 3;
    public const int ReceiveTx = 4;
    public const int ResendTx = 9;
}

public class TonResendTypeConstants
{
    public const int IntervalSeconds = 1;
}

public class TonStringConstants
{
    public const string Value = "value";
    public const string MessageValue = "message_hash";
    public const string RunGetMethod = "runGetMethod";
    public const string TonIndexerStorageKey = "TonIndexer";
    public const string TonTaskStorageKey = "TonTask";
    public const string Seqno = "seqno";
    public const string Error = "error";
    public const string TonCenter = "TonCenter";
    public const string GetBlock = "GetBlock";
    public const string TonApi = "TonApi";
    public const string ChainStack = "ChainStack";
}