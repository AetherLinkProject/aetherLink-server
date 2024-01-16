namespace AetherLink.Worker.Core.Dtos;

public enum RequestState
{
    RequestPending,
    RequestStart,
    ObservationResultCommitted,
    ReportGenerated,
    ReportSigned,
    RequestTransmitted,
    RequestEnd,
    RequestCanceled,
    Transmitted
}