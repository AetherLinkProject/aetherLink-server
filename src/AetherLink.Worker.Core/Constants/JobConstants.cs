namespace AetherLink.Worker.Core.Constants;

public static class RequestTypeConst
{
    public const int Datafeeds = 1;
    public const int Vrf = 2;
}

public enum DataFeedsType
{
    PriceFeeds,
    NftFeeds
}

public enum SchedulerType
{
    CheckRequestEndScheduler,
    ObservationCollectWaitingScheduler
}