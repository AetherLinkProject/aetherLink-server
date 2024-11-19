namespace AetherLink.Server.HttpApi.Dtos;

public class BasicResponseDto<T>
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = "";
    public T Data { get; set; }
}