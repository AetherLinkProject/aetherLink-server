namespace AetherLink.Server.HttpApi.Dtos;

public class BasicResponseDto<T>
{
    public bool success { get; set; }
    public string message { get; set; }
    public T Data { get; set; }
}