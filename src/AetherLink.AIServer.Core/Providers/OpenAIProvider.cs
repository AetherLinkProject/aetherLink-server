using System;
using System.ClientModel;
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
using OpenAI.Images;

namespace AetherLink.AIServer.Core.Providers;

public interface IOpenAIProvider
{
    Task<OpenAIResponse> GetCompletionAsync(OpenAIRequest request);
    Task<string> GetDallEAsync(string prompt);
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

    public async Task<string> GetDallEAsync(string prompt)
    {
        try
        {
            var client = new ImageClient("dall-e-3", _openAiOption.Secret);
            GeneratedImage image = await client.GenerateImageAsync(prompt, new()
            {
                // Quality = GeneratedImageQuality.Standard,
                // Style = GeneratedImageStyle.Vivid,
                Size = GeneratedImageSize.W1024xH1024,
                ResponseFormat = GeneratedImageFormat.Bytes
            });

            var result = Convert.ToBase64String(image.ImageBytes);
            _logger.LogDebug($"Get Picture result: {result}");
            return result;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}