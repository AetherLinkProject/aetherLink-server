using System;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using AetherLink.Indexer;
using AetherLink.Indexer.Provider;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Nethereum.ABI.FunctionEncoding.Attributes;
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
        var receivedMessage = new EvmReceivedMessageDto
        {
            MessageId = sendRequestData.MessageId.ToHex(),
            Sender = Convert.ToBase64String(Encoding.UTF8.GetBytes(sendRequestData.Sender)),
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
        return receivedMessage;
    }
}

[Event("RequestSent")]
public class SendEventDTO : IEventDTO
{
    [Parameter("bytes32", "messageId", 1, true)]
    public byte[] MessageId { get; set; }

    [Parameter("uint256", "messageId", 2, false)]
    public BigInteger Epoch { get; set; }

    [Parameter("address", "sender", 3, true)]
    public string Sender { get; set; }

    [Parameter("string", "receiver", 4, false)]
    public string Receiver { get; set; }

    [Parameter("uint256", "sourceChainId", 5, false)]
    public BigInteger SourceChainId { get; set; }

    [Parameter("uint256", "targetChainId", 6, false)]
    public BigInteger TargetChainId { get; set; }

    [Parameter("bytes", "message", 7, false)]
    public byte[] Message { get; set; }

    [Parameter("string", "targetContractAddress", 8, false)]
    public string TargetContractAddress { get; set; }

    [Parameter("string", "tokenAddress", 9, false)]
    public string TokenAddress { get; set; }

    [Parameter("uint256", "amount", 10, false)]
    public BigInteger Amount { get; set; }
}

[Event("Transmitted")]
public class TransmitEventDTO : IEventDTO
{
    [Parameter("string", "requestId", 1, true)]
    public string RequestId { get; set; }

    [Parameter("address", "sender", 2, true)]
    public string Sender { get; set; }

    [Parameter("uint256", "targetChain", 3, true)]
    public BigInteger TargetChain { get; set; }

    [Parameter("address", "targetContract", 4, true)]
    public string Receiver { get; set; }

    [Parameter("bytes", "data", 5, false)] public byte[] Data { get; set; }

    [Parameter("bytes", "tokenAmounts", 6, false)]
    public byte[] TokenAmounts { get; set; }
}