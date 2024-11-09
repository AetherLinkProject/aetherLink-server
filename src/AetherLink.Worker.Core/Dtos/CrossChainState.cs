namespace AetherLink.Worker.Core.Dtos;

public enum CrossChainState
{
    RequestStart = 1,
    Committed,
    Confirmed,
    PendingResend,
    RequestCanceled
}