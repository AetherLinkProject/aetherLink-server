namespace AetherLink.Server.HttpApi.Dtos;

public enum CrossChainStatus
{
    Started,
    PendingCommit,
    PendingResend,
    Committed
}