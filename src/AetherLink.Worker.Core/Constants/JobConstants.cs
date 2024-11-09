namespace AetherLink.Worker.Core.Constants;

public static class RequestTypeConst
{
    public const int Datafeeds = 1;
    public const int Vrf = 2;
    public const int Automation = 3;
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

public enum UpkeepSchedulerType
{
    CheckCronUpkeepEndScheduler
}

public enum RampSchedulerType
{
    CheckCommittedScheduler,
    ResendPendingScheduler
}

public enum CrossChainSchedulerType
{
    CheckCommittedScheduler,
    ResendPendingScheduler
}