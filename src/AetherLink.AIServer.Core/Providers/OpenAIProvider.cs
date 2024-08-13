using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AetherLink.AIServer.Core.Dtos;
using AetherLink.AIServer.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;
using OpenAI.Chat;

namespace AetherLink.AIServer.Core.Providers;

public interface IOpenAIProvider
{
    Task<OpenAIResponse> GetCompletionAsync(OpenAIRequest request);
}

public class OpenAIProvider : IOpenAIProvider, ISingletonDependency
{
    private readonly SemaphoreSlim _semaphore;
    private readonly OpenAIOption _openAiOption;
    private readonly ILogger<OpenAIProvider> _logger;

    public OpenAIProvider(IOptions<OpenAIOption> openAiOption, ILogger<OpenAIProvider> logger)
    {
        _logger = logger;
        _openAiOption = openAiOption.Value;
        _semaphore = new SemaphoreSlim(1, _openAiOption.RequestLimit);
    }

    public async Task<OpenAIResponse> GetCompletionAsync(OpenAIRequest request)
    {
        await _semaphore.WaitAsync();
        var input = BinaryData.FromObjectAsJson(request,
            new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        try
        {
            var client = new ChatClient("gpt-4o", _openAiOption.Secret);
            var content = BinaryContent.Create(input);
            var result = await client.CompleteChatAsync(content);
            var output = result.GetRawResponse().Content;
            _logger.LogDebug(output.ToString());
            var openAIResponse = JsonConvert.DeserializeObject<OpenAIResponse>(output.ToString());
            _logger.LogDebug(openAIResponse.Choices[0].Message.Role);
            _logger.LogDebug(openAIResponse.Choices[0].Message.Content);
            return openAIResponse;
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Get OpenAI response failed with {input}.");
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}