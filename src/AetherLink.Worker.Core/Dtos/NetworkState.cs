namespace AetherLink.Worker.Core.Dtos;

public class NetworkState
{
    public bool IsWsRunning { get; set; }
    public long LastProcessedBlock { get; set; }
}