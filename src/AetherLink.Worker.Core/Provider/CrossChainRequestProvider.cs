using System;
using System.Threading.Tasks;
using AElf;
using AElf.Types;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Nethereum.Hex.HexConvertors.Extensions;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.Provider;

public interface ICrossChainRequestProvider
{
    public Task StartCrossChainRequestFromTon(ReceiveMessageDto request);
    public Task StartCrossChainRequestFromAELf(RampRequestDto request);

    public Task SetAsync(CrossChainDataDto data);
    public Task<CrossChainDataDto> GetAsync(string messageId);
}

public class CrossChainRequestProvider : ICrossChainRequestProvider, ITransientDependency
{
    private readonly ITokenSwapper _tokenSwapper;
    private readonly IObjectMapper _objectMapper;
    private readonly IStorageProvider _storageProvider;
    private readonly ILogger<CrossChainRequestProvider> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public CrossChainRequestProvider(IBackgroundJobManager backgroundJobManager, ITokenSwapper tokenSwapper,
        ILogger<CrossChainRequestProvider> logger, IStorageProvider storageProvider, IObjectMapper objectMapper)
    {
        _logger = logger;
        _tokenSwapper = tokenSwapper;
        _storageProvider = storageProvider;
        _objectMapper = objectMapper;
        _backgroundJobManager = backgroundJobManager;
    }

    public async Task StartCrossChainRequestFromTon(ReceiveMessageDto request)
    {
        _logger.LogDebug("[CrossChainRequestProvider] Start CrossChainRequest From Ton....");
        await _backgroundJobManager.EnqueueAsync(new CrossChainRequestStartArgs
        {
            ReportContext = new()
            {
                MessageId = request.MessageId,
                Sender = request.Sender,
                // Receiver = Address.FromPublicKey("BBB".HexToByteArray()).ToByteString().ToBase64(),
                Receiver = request.TargetContractAddress,
                // Receiver = ByteString.CopyFromUtf8("EQCM07L_gOFQYakjtELvsJoeHXgEHgmNdvnPKwuY8Yv-XQMi").ToBase64(),
                TargetChainId = request.TargetChainId,
                // TargetChainId = 1100,
                SourceChainId = request.SourceChainId,
                Epoch = request.Epoch
            },
            Message = request.Message,
            TokenAmount = await _tokenSwapper.ConstructSwapId(request.TokenAmountInfo),
            StartTime = request.TransactionTime
        });
    }

    public async Task StartCrossChainRequestFromAELf(RampRequestDto request)
    {
        _logger.LogDebug($"[CrossChainRequestProvider] Start CrossChainRequest From {request.ChainId}....");
        await _backgroundJobManager.EnqueueAsync(new CrossChainRequestStartArgs
        {
            ReportContext = new()
            {
                MessageId = request.MessageId,
                Sender = request.Sender,
                Receiver = ByteString.CopyFromUtf8("EQCM07L_gOFQYakjtELvsJoeHXgEHgmNdvnPKwuY8Yv-XQMi").ToBase64(),
                TargetChainId = request.TargetChainId,
                SourceChainId = request.SourceChainId,
                Epoch = request.Epoch
            },
            Message = request.Message,
            TokenAmount = await _tokenSwapper.ConstructSwapId(new()
            {
                TargetChainId = 1100,
                TargetContractAddress = "test_target_address",
                TokenAddress = "test_token_address",
                OriginToken = "test_origin_token",
                Amount = 100
            }),
            StartTime = request.StartTime
        });
    }

    public async Task SetAsync(CrossChainDataDto data)
    {
        var key = GenerateCrossChainDataId(data.ReportContext.MessageId);

        _logger.LogDebug("[CrossChainRequestProvider] Start to set {key}", key);

        await _storageProvider.SetAsync(key, data);
    }

    public async Task<CrossChainDataDto> GetAsync(string messageId)
        => await _storageProvider.GetAsync<CrossChainDataDto>(GenerateCrossChainDataId(messageId));

    private static string GenerateCrossChainDataId(string messageId)
        => IdGeneratorHelper.GenerateId(RedisKeyConstants.CrossChainDataKey, messageId);
}