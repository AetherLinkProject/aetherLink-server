using System;
using System.Threading.Tasks;
using AElf;
using AetherLink.Indexer.Dtos;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Nethereum.Util;
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
            crossChainRequestStartArgs.TokenTransferMetadata =
                await _tokenSwapper.ConstructSwapId(crossChainRequestStartArgs.ReportContext,
                    request.TokenTransferMetadataDtoInfo);
            await _backgroundJobManager.EnqueueAsync(crossChainRequestStartArgs);
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                $"[CrossChainRequestProvider] Start cross chain request from TON failed, messageId: {request.MessageId}");
        }
    }

    public async Task StartCrossChainRequestFromEvm(EvmReceivedMessageDto request)
    {
        try
        {
            _logger.LogDebug("[CrossChainRequestProvider] Start CrossChainRequest From EVM....");
            var startArgs = new CrossChainRequestStartArgs
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
                StartTime = request.TransactionTime
            };
            startArgs.TokenTransferMetadata =
                await _tokenSwapper.ConstructSwapId(startArgs.ReportContext, request.TokenTransferMetadataInfo);
            await _backgroundJobManager.EnqueueAsync(startArgs);
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                $"[CrossChainRequestProvider] Start cross chain request from EVM failed, messageId: {request.MessageId}");
        }
    }

    public async Task StartCrossChainRequestFromAELf(RampRequestDto request)
    {
        try
        {
            _logger.LogDebug(
                $"[CrossChainRequestProvider] Start CrossChainRequest From {request.ChainId} transactionId: {request.TransactionId}");
            var crossChainRequestStartArgs = new CrossChainRequestStartArgs
            {
                Message = request.Message,
                StartTime = request.StartTime,
                ReportContext = ContextPreprocessing(request)
            };

            if (request.TokenTransferMetadata is { TargetChainId: > 0 } &&
                !string.IsNullOrEmpty(request.TokenTransferMetadata.Symbol))
            {
                crossChainRequestStartArgs.TokenTransferMetadata = await _tokenSwapper.ConstructSwapId(
                    crossChainRequestStartArgs.ReportContext, new()
                    {
                        TargetChainId = (long)request.TokenTransferMetadata.TargetChainId,
                        Symbol = request.TokenTransferMetadata.Symbol,
                        Amount = (long)request.TokenTransferMetadata.Amount
                    });
            }

            await _backgroundJobManager.EnqueueAsync(crossChainRequestStartArgs);
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                $"[CrossChainRequestProvider] Start cross chain request from AELF failed, transactionId: {request.TransactionId} messageId: {request.MessageId}");
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

    private ReportContextDto ContextPreprocessing(RampRequestDto request)
    {
        var reportContext = new ReportContextDto()
        {
            MessageId = request.MessageId,
            Sender = request.Sender,
            TargetChainId = request.TargetChainId,
            SourceChainId = request.SourceChainId,
            Epoch = request.Epoch
        };

        switch (reportContext.TargetChainId)
        {
            case ChainIdConstants.EVM:
            case ChainIdConstants.BSC:
            case ChainIdConstants.BSCTEST:
            case ChainIdConstants.SEPOLIA:
            case ChainIdConstants.BASESEPOLIA:
                var checksumAddress = new AddressUtil().ConvertToChecksumAddress(
                    ByteString.FromBase64(request.Receiver).ToHex(true));
                reportContext.Receiver = checksumAddress;
                break;
            default:
                reportContext.Receiver = request.Receiver;
                break;
        }

        return reportContext;
    }
}