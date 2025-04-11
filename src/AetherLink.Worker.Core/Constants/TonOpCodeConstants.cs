namespace AetherLink.Worker.Core.Constants;

public class TonEnvConstants
{
    public const long ResendMaxWaitSeconds = 30;
    public const int PerCellStorageBytesCount = 32 * 3;
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
    public const string TonIndexerStorageKey = "TonIndexer";
    public const string TonCenterLatestBlockInfoKey = "TonCenterBlockInfo";
}

public class TonHttpApiUriConstants
{
    public const string RunGetMethod = "/runGetMethod";
    public const string SendTransaction = "/sendBoc";
    public const string MasterChainInfo = "/api/v3/masterchainInfo";
    public const string GetTransactions = "/api/v3/transactions";
}