namespace AetherLink.AIServer.Core.Dtos;

public class RequestStorageDto
{
    public string Commitment { get; set; }
    public string RequestId { get; set; }
    public object Status { get; set; }
    public string Id { get; set; }
}

public enum RequestType
{
    Started,
    Signed,
    Transmitted,
    Finished
}