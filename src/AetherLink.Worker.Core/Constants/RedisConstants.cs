namespace AetherLink.Worker.Core.Constants;

public static class RedisKeyConstants
{
    public const string JobKey = "Job";
    public const string VrfJobKey = "VrfJob";
    public const string ReportKey = "Report";
    public const string DataMessageKey = "DataMessage";
    public const string CrossChainDataKey = "crosschaindata";
    public const string TonEpochStorageKey = "TonEpochStorageKey";
    public const string SearchHeightKey = "SearchHeight";
    public const string PlainDataFeedsKey = "PlainDataFeeds";
    public const string UnconfirmedSearchHeightKey = "UnconfirmedSearchHeight";

    // poller
    public const string LookBackBlocksKey = "LookBackBlocks";

    // filter
    public const string EventFiltersKey = "EventFilters";
    public const string TransactionEventKey = "TransactionEvent";

    // automation
    public const string UpkeepInfoKey = "UpkeepInfo";
    public const string UpkeepLogTriggerInfoKey = "UpkeepLogTriggerInfo";
}

public static class RedisNetworkConstants
{
    public const int DefaultScanDelayTime = 100;
    public const int DefaultScanStep = 100;
    public const string ScanCommand = "SCAN";
}