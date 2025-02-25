using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using AetherLink.Indexer.Provider;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.Logging;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding;
using Volo.Abp.DependencyInjection;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;
using Nethereum.Contracts;

namespace AetherLink.Worker.Core.Service;

public interface IEvmSearchServer
{
    Task StartAsync();
}

public class EvmSearchServer : IEvmSearchServer, ISingletonDependency
{
    private readonly ILogger<EvmSearchServer> _logger;
    private readonly IInfuraRpcProvider _indexerProvider;
    private readonly ICrossChainRequestProvider _crossChainProvider;

    public EvmSearchServer(IInfuraRpcProvider indexerProvider, ILogger<EvmSearchServer> logger,
        ICrossChainRequestProvider crossChainProvider)
    {
        _logger = logger;
        _indexerProvider = indexerProvider;
        _crossChainProvider = crossChainProvider;
    }

    public Task StartAsync()
    {
        _logger.LogDebug("[EvmSearchServer] Starting EvmSearchServer ....");
        return Task.Run(async () =>
        {
            var tasks = new List<Task>();

            tasks.Add(SubscribeRequestAsync());
            // tasks.Add(SubscribeTransmitAsync());

            await Task.WhenAll(tasks);
        });
    }

    private async Task SubscribeRequestAsync()
    {
        try
        {
            await _indexerProvider.SubscribeAndRunAsync<SendEventDTO>(
                eventData =>
                {
                    _logger.LogInformation("[EvmSearchServer] Received Event --> ");
                    _crossChainProvider.StartCrossChainRequestFromEvm(GenerateEvmReceivedMessage(eventData));
                });

            _logger.LogInformation("[EvmSearchServer] Start handler cross chain request... ");
            // await _crossChainProvider.StartCrossChainRequestFromEvm(new()
            // {
            //     MessageId = "testReceivedMessage",
            //     Sender = "test_evm_address",
            //     Epoch = 0,
            //     SourceChainId = ChainIdConstants.EVM,
            //     TargetChainId = ChainIdConstants.AELF,
            //     Receiver = "test_aelf_address",
            //     TransactionTime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds(),
            //     Message = ""
            // });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private async Task SubscribeTransmitAsync()
    {
        try
        {
            await _indexerProvider.SubscribeAndRunAsync<TransmitEventDTO>(
                eventData =>
                {
                    _logger.LogInformation("[EvmSearchServer] Received transmit Event --> ");
                    // _crossChainProvider.StartCrossChainRequestFromEvm(GenerateEvmReceivedMessage(eventData));
                });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private EvmReceivedMessageDto GenerateEvmReceivedMessage(EventLog<SendEventDTO> eventData)
    {
        var tokenAmount = eventData.Event.DecodeTokenAmount();

        var blockNumber = eventData.Log.BlockNumber;
        var receivedMessage = new EvmReceivedMessageDto
        {
            MessageId = "testReceivedMessage",
            Sender = "test_evm_address",
            Epoch = 0,
            SourceChainId = ChainIdConstants.EVM,
            TargetChainId = ChainIdConstants.AELF,
            BlockNumber = blockNumber,
            Receiver = "test_aelf_address",
            Message = null,
            TokenAmountInfo = null
        };
        return receivedMessage;
    }
}

[Event("RequestSent")]
public class SendEventDTO : IEventDTO
{
    [Parameter("bytes32", "messageId", 1, true)]
    public byte[] MessageId { get; set; }

    [Parameter("address", "sender", 2, true)]
    public string Sender { get; set; }

    [Parameter("string", "receiver", 3, false)]
    public string Receiver { get; set; }

    [Parameter("uint256", "targetChainId", 4, false)]
    public BigInteger TargetChainId { get; set; }

    [Parameter("bytes", "message", 5, false)]
    public byte[] Message { get; set; }

    // [Parameter("bytes", "tokenAmount", 5, false)]
    // public byte[] TokenAmount { get; set; }
    [Parameter("bytes", "tokenAmount", 6, false)]
    public byte[] TokenAmountBytes { get; set; }

    public TokenAmount DecodeTokenAmount()
    {
        var decoder = new ParameterDecoder();
        var tokenAmount = new TokenAmount();
        var properties = typeof(TokenAmount).GetProperties();

        decoder.DecodeAttributes(TokenAmountBytes, tokenAmount, properties);
        return tokenAmount;
    }
}

[FunctionOutput]
public class TokenAmount
{
    [Parameter("string", "swapId", 1)] public string SwapId { get; set; }

    [Parameter("uint256", "targetChainId", 2)]
    public BigInteger TargetChainId { get; set; }

    [Parameter("string", "targetContractAddress", 3)]
    public string TargetContractAddress { get; set; }

    [Parameter("string", "tokenAddress", 4)]
    public string TokenAddress { get; set; }

    [Parameter("string", "originToken", 5)]
    public string OriginToken { get; set; }

    [Parameter("uint256", "amount", 6)] public BigInteger Amount { get; set; }
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