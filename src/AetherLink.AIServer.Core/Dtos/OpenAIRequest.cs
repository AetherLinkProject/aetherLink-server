using System.Collections.Generic;

namespace AetherLink.AIServer.Core.Dtos;

public class OpenAIRequest
{
    public string Model { get; set; } = "gpt-4o";
    public List<Message> Messages { get; set; }
}