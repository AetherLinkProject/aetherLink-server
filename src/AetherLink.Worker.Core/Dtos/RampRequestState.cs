namespace AetherLink.Worker.Core.Dtos;

public enum RampRequestState
{
    RequestStart = 1,
    Committed,
    PendingResend
}