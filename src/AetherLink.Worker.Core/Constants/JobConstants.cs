namespace AetherLink.Worker.Core.Constants;

public static class RequestTypeConst
{
    public const int Datafeeds = 1;
    public const int Vrf = 2;
}

public enum DataFeedsType
{
    PriceFeeds,
    PlainDataFeeds
}

public enum SchedulerType
{
    CheckRequestEndScheduler,
    ObservationCollectWaitingScheduler
}