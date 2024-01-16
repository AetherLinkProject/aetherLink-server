namespace AetherLink.Worker.Core.Dtos;

public class DataMessageDto : RequestBase
{
    public long Data { get; set; }
    public int Index { get; set; }
}