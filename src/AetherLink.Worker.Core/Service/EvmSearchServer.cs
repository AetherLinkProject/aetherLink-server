using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AetherLink.Indexer;
using AetherLink.Indexer.Dtos;
using AetherLink.Indexer.Provider;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;

namespace AetherLink.Worker.Core.Service;

public interface IEvmSearchServer
{
    Task StartAsync();
}

public class EvmSearchServer : IEvmSearchServer, ISingletonDependency
{
    private readonly ILogger<EvmSearchServer> _logger;
    private readonly IEvmRpcProvider _indexerProvider;
    private readonly EvmIndexerOptionsMap _networkOptions;
    private readonly ICrossChainRequestProvider _crossChainProvider;

    public EvmSearchServer(ILogger<EvmSearchServer> logger, ICrossChainRequestProvider crossChainProvider,
        IEvmRpcProvider indexerProvider, IOptionsSnapshot<EvmIndexerOptionsMap> networkOptions)
    {
        _logger = logger;
        _indexerProvider = indexerProvider;
        _networkOptions = networkOptions.Value;
        _crossChainProvider = crossChainProvider;
    }

    public async Task StartAsync()
    {
        _logger.LogDebug("[EvmSearchServer] Starting EvmSearchServer ....");
        await Task.WhenAll(_networkOptions.ChainInfos.Values.Select(SubscribeRequestAsync));
    }

    private async Task SubscribeRequestAsync(EvmIndexerOptions options)
    {
        try
        {
            await _indexerProvider.SubscribeAndRunAsync<SendEventDTO>(
                options, eventData =>
                {
                    _logger.LogInformation("[EvmSearchServer] Received Event --> ");
                    _crossChainProvider.StartCrossChainRequestFromEvm(GenerateEvmReceivedMessage(eventData));
                });

            _logger.LogInformation("[EvmSearchServer] Start handler cross chain request... ");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[EvmSearchServer] Subscribe cross chain request fail.");
            throw;
        }
    }

    private EvmReceivedMessageDto GenerateEvmReceivedMessage(EventLog<SendEventDTO> eventData)
    {
        var blockNumber = eventData.Log.BlockNumber;
        var sendRequestData = eventData.Event;
        var messageId = sendRequestData.MessageId.ToHex();
        var sender = Convert.ToBase64String(Encoding.UTF8.GetBytes(sendRequestData.Sender));
        var receivedMessage = new EvmReceivedMessageDto
        {
            MessageId = messageId,
            Sender = sender,
            Epoch = (long)sendRequestData.Epoch,
            SourceChainId = (long)sendRequestData.SourceChainId,
            TargetChainId = (long)sendRequestData.TargetChainId,
            BlockNumber = blockNumber,
            Receiver = sendRequestData.Receiver,
            Message = Convert.ToBase64String(sendRequestData.Message),
            TokenAmountInfo = new()
            {
                TargetChainId = (long)sendRequestData.TargetChainId,
                TargetContractAddress = sendRequestData.TargetContractAddress,
                TokenAddress = sendRequestData.TokenAddress,
                Amount = (long)sendRequestData.Amount
            },
            TransactionTime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds()
        };

        _logger.LogInformation(
            $"[EvmSearchServer] Get evm cross chain request {messageId} from {sender} to {(long)sendRequestData.TargetChainId} {sendRequestData.Receiver}");
        return receivedMessage;
    }
}