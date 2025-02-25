using System;
using System.Threading.Tasks;
using AetherLink.Indexer.Dtos;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface ICrossChainRequestProvider
{
    public Task StartCrossChainRequestFromTon(ReceiveMessageDto request);
    public Task StartCrossChainRequestFromEvm(EvmReceivedMessageDto request);
    public Task StartCrossChainRequestFromAELf(RampRequestDto request);

    public Task SetAsync(CrossChainDataDto data);
    public Task<CrossChainDataDto> GetAsync(string messageId);
}

public class CrossChainRequestProvider : ICrossChainRequestProvider, ITransientDependency
{
    private readonly ITokenSwapper _tokenSwapper;
    private readonly IStorageProvider _storageProvider;
    private readonly ILogger<CrossChainRequestProvider> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public CrossChainRequestProvider(IBackgroundJobManager backgroundJobManager, ITokenSwapper tokenSwapper,
        ILogger<CrossChainRequestProvider> logger, IStorageProvider storageProvider)
    {
        _logger = logger;
        _tokenSwapper = tokenSwapper;
        _storageProvider = storageProvider;
        _backgroundJobManager = backgroundJobManager;
    }

    public async Task StartCrossChainRequestFromTon(ReceiveMessageDto request)
    {
        try
        {
            _logger.LogDebug("[CrossChainRequestProvider] Start CrossChainRequest From Ton....");
            var crossChainRequestStartArgs = new CrossChainRequestStartArgs
            {
                ReportContext = new()
                {
                    MessageId = request.MessageId,
                    Sender = request.Sender,
                    Receiver = request.TargetContractAddress,
                    TargetChainId = request.TargetChainId,
                    SourceChainId = request.SourceChainId,
                    Epoch = request.Epoch
                },
                Message = request.Message,
                StartTime = request.TransactionTime
            };
            crossChainRequestStartArgs.TokenAmount =
                await _tokenSwapper.ConstructSwapId(crossChainRequestStartArgs.ReportContext, request.TokenAmountInfo);
            await _backgroundJobManager.EnqueueAsync(crossChainRequestStartArgs);
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                $"[CrossChainRequestProvider] Start cross chain request from ton failed, messageId: {request.MessageId}");
        }
    }

    public async Task StartCrossChainRequestFromEvm(EvmReceivedMessageDto request)
    {
        try
        {
            _logger.LogDebug("[CrossChainRequestProvider] Start CrossChainRequest From EVM....");
            var crossChainRequestStartArgs = new CrossChainRequestStartArgs
            {
                ReportContext = new()
                {
                    MessageId = request.MessageId,
                    Sender = request.Sender,
                    Receiver = request.Receiver,
                    TargetChainId = request.TargetChainId,
                    SourceChainId = request.SourceChainId,
                    Epoch = request.Epoch
                },
                Message = request.Message,
                StartTime = request.TransactionTime,
                TokenAmount = new()
                {
                    SwapId = "e27025bb839682c3e55d4e4a415a32242529acaebe74bb2e3213111c3eae14b2",
                    TargetChainId = ChainIdConstants.AELF,
                    TargetContractAddress = "2TL2DvyGhiwyxaP6KQYkgF1VgwcrurH22u3kaDvc4iwYMLEngn",
                    TokenAddress = "0x8adD57b8aD6C291BC3E3ffF89F767fcA08e0E7Ab",
                    OriginToken = "ELF",
                    Amount = 100
                }
            };
            // crossChainRequestStartArgs.TokenAmount =
            //     await _tokenSwapper.ConstructSwapId(crossChainRequestStartArgs.ReportContext, request.TokenAmountInfo);
            await _backgroundJobManager.EnqueueAsync(crossChainRequestStartArgs);
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                $"[CrossChainRequestProvider] Start cross chain request from ton failed, messageId: {request.MessageId}");
        }
    }

    public async Task StartCrossChainRequestFromAELf(RampRequestDto request)
    {
        // todo: for debug, skip ton crossChain
        if (request.TargetChainId == 1100) return;

        try
        {
            _logger.LogDebug($"[CrossChainRequestProvider] Start CrossChainRequest From {request.ChainId}....");
            var crossChainRequestStartArgs = new CrossChainRequestStartArgs
            {
                ReportContext = new()
                {
                    MessageId = request.MessageId,
                    Sender = request.Sender,
                    Receiver = request.Receiver,
                    TargetChainId = request.TargetChainId,
                    SourceChainId = request.SourceChainId,
                    Epoch = request.Epoch
                },
                Message = request.Message,
                StartTime = request.StartTime
            };
            crossChainRequestStartArgs.TokenAmount = await _tokenSwapper.ConstructSwapId(
                crossChainRequestStartArgs.ReportContext, new()
                {
                    TargetChainId = request.TokenAmount.TargetChainId,
                    TargetContractAddress = request.TokenAmount.TargetContractAddress,
                    OriginToken = request.TokenAmount.OriginToken
                });

            await _backgroundJobManager.EnqueueAsync(crossChainRequestStartArgs);
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                $"[CrossChainRequestProvider] Start cross chain request from aelf failed, transactionId: {request.TransactionId} messageId: {request.MessageId}");
        }
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