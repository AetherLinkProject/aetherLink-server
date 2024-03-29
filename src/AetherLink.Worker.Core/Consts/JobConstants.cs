namespace AetherLink.Worker.Core.Consts;

public static class RequestTypeConst
{
    public const int Datafeeds = 1;
    public const int Vrf = 2;
    public const int Transmitted = -1;
    public const int RequestedCancel = -2;
}

public enum DataFeedsType
{
    PriceFeeds,
    NftFeeds
}

public enum SchedulerType
{
    CheckRequestReceiveScheduler,
    CheckObservationResultCommitScheduler,
    CheckReportReceiveScheduler,
    CheckReportCommitScheduler,
    CheckTransmitScheduler,
    CheckRequestEndScheduler
}