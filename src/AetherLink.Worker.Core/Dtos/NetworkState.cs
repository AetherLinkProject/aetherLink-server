namespace AetherLink.Worker.Core.Dtos;

public class NetworkState
{
    public bool IsWsRunning { get; set; }
    public bool IsHttpFinished { get; set; }
    public long LastProcessedBlock { get; set; }
}