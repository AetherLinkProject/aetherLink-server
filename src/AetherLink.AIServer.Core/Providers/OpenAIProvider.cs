using System.Net.Http;
using System.Threading.Tasks;

namespace AetherLink.AIServer.Core.Providers;

public interface IOpenAIProvider
{
}

public class OpenAIProvider : IOpenAIProvider
{
    private readonly IHttpClientFactory _httpFactory;

    public OpenAIProvider(IHttpClientFactory httpFactory)
    {
        _httpFactory = httpFactory;
    }

    public async Task GetCompletionAsync(OpenAIRequest request)
    {
        var result = await _httpFactory.CreateClient().GetStringAsync("");
        // var client = new OpenAIConfiguration(apiKey: apiKey).GetClient();
    }
}

public class OpenAIRequest
{
}