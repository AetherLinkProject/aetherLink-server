namespace AetherLink.Worker.Core.Dtos;

public class DataMessageDto : OracleRequestBase
{
    public long Data { get; set; }
    public int Index { get; set; }
}