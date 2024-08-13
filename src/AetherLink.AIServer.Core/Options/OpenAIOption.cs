namespace AetherLink.AIServer.Core.Options;

public class OpenAIOption
{
    public string Secret { get; set; }
    public int RequestLimit { get; set; } = 1;
}