using System;
using System.Threading.Tasks;
using AElf;
using AElf.Cryptography;
using AetherLink.AIServer.Core.Dtos;
using AetherLink.AIServer.Core.Options;
using AetherLink.AIServer.Core.Providers;
using AetherLink.Contracts.AIFeeds;
using Ai;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.AIServer.Core.Enclave;

public interface IEnclaveManager
{
    Task<RequestStorageDto> CreateAsync(AIRequestDto request);
    Task<(ByteString, byte[])> ProcessAsync(OracleContext ctx, RequestStorageDto data);
    Task FinishAsync(AIReportTransmittedDto transmitted);
}

public class EnclaveManager : IEnclaveManager, ISingletonDependency
{
    private readonly EnclaveOption _option;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<EnclaveManager> _logger;
    private readonly IOpenAIProvider _openAIProvider;
    private readonly IRequestProvider _requestProvider;

    public EnclaveManager(ILogger<EnclaveManager> logger, IOpenAIProvider openAiProvider,
        IOptions<EnclaveOption> option,
        IRequestProvider requestProvider,
        IObjectMapper objectMapper)
    {
        _logger = logger;
        _option = option.Value;
        _objectMapper = objectMapper;
        _openAIProvider = openAiProvider;
        _requestProvider = requestProvider;
    }

    public async Task<RequestStorageDto> CreateAsync(AIRequestDto request)
    {
        var data = _objectMapper.Map<AIRequestDto, RequestStorageDto>(request);
        var id = GenerateRequestId(request);
        data.Id = id;
        data.Status = RequestType.Started;
        await _requestProvider.SetAsync(id, data);

        return data;
    }

    public async Task FinishAsync(AIReportTransmittedDto transmitted)
    {
        var id = GenerateRequestId(transmitted);
        var request = await _requestProvider.GetAsync(id);
        request.Status = RequestType.Finished;
        await _requestProvider.SetAsync(id, request);
    }

    public async Task<(ByteString, byte[])> ProcessAsync(OracleContext ctx, RequestStorageDto data)
    {
        var r = AIRequest.Parser.ParseFrom(ByteString.FromBase64(data.Commitment));
        _logger.LogDebug($"[EnclaveManager] {r.Description.Detail.ToStringUtf8()}");

        ByteString report;
        switch (r.Model)
        {
            case ModelType.ChatGpt:
                var chatGptResponse = await _openAIProvider.GetCompletionAsync(new()
                {
                    Model = "gpt-4o",
                    Messages = new()
                    {
                        new()
                        {
                            Role = "user",
                            Content = r.Description.Detail.ToStringUtf8()
                        }
                    }
                });

                report = new ChatGptResponse
                {
                    Model = chatGptResponse.Model,
                    Content = ByteString.CopyFromUtf8(chatGptResponse.Choices[0].Message.Content)
                }.ToByteString();

                break;
            case ModelType.Dall:
                var dallResponse = await _openAIProvider.GetDallEAsync(r.Description.Detail.ToStringUtf8());

                report = new ChatGptResponse
                {
                    Model = ModelType.Dall.ToString(),
                    Content = ByteString.CopyFromUtf8(dallResponse)
                }.ToByteString();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        var signedResult = SignReport(report, ctx);
        data.Status = RequestType.Signed;
        await _requestProvider.SetAsync(data.Id, data);
        return (report, signedResult);
    }

    private byte[] SignReport(ByteString report, OracleContext ctx)
    {
        var payload = HashHelper.ConcatAndCompute(
            HashHelper.ComputeFrom(report.ToByteArray()),
            HashHelper.ComputeFrom(ctx.ToString())).ToByteArray();
        return CryptoHelper.SignWithPrivateKey(ByteArrayHelper.HexStringToByteArray(_option.PrivateKey), payload);
    }

    private string GenerateRequestId(AIRequestDto data) => data.ChainId + "-" + data.RequestId;
    private string GenerateRequestId(AIReportTransmittedDto data) => data.ChainId + "-" + data.RequestId;
}