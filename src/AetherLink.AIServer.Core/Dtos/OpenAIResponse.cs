namespace AetherLink.AIServer.Core.Dtos;

public class OpenAIResponse
{
    public string Id { get; set; }
    public string Object { get; set; }
    public long Created { get; set; }
    public string Model { get; set; }
    public Choice[] Choices { get; set; }
    public Usage Usage { get; set; }
    public string ErrorMessage { get; set; }
}

public class Choice
{
    public Message Message { get; set; }
    public int Index { get; set; }
    public Logprobs Logprobs { get; set; }
    public string FinishReason { get; set; }
}

public class Logprobs
{
    public double[] Tokens { get; set; }
    public double[] TokenLogprobs { get; set; }
    public int[] TopLogprobs { get; set; }
}

public class Usage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}