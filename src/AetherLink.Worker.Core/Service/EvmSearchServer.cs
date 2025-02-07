using System.Threading.Tasks;
using AetherLink.Indexer.Provider;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Math;
using Volo.Abp.DependencyInjection;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace AetherLink.Worker.Core.Service;

public interface IEvmSearchServer
{
    Task StartAsync();
}

public class EvmSearchServer : IEvmSearchServer, ISingletonDependency
{
    private readonly ILogger<EvmSearchServer> _logger;
    private readonly IInfuraRpcProvider _indexerProvider;
    private readonly ICrossChainRequestProvider _crossChainRequestProvider;

    public EvmSearchServer(IInfuraRpcProvider indexerProvider, ILogger<EvmSearchServer> logger,
        ICrossChainRequestProvider crosschainRequestProvider)
    {
        _logger = logger;
        _indexerProvider = indexerProvider;
        _crossChainRequestProvider = crosschainRequestProvider;
    }

    public Task StartAsync()
    {
        _logger.LogDebug("[EvmSearchServer] Starting EvmSearchServer ....");
        return Task.Run(async () =>
        {
            await _indexerProvider.SubscribeAndRunAsync<SendEventDTO>(
                eventData =>
                {
                    _logger.LogInformation("[EvmSearchServer] Received Event --> ");
                    _crossChainRequestProvider.StartCrossChainRequestFromEvm(GenerateEvmReceivedMessage(eventData));
                    // $"[EvmSearchServer] Received Event --> ReceiptId: {eventData.ReceiptId} Asset: {eventData.Asset} Owner: {eventData.Owner} Amount: {eventData.Amount}");
                });
        });
    }

    private EvmReceivedMessageDto GenerateEvmReceivedMessage(SendEventDTO eventData)
    {
        var receivedMessage = new EvmReceivedMessageDto();
        return receivedMessage;
    }
}

// [Event("NewReceipt")]
// public class NewReceiptEventDTO : IEventDTO
// {
//     [Parameter("string", "receiptId", 1, true)]
//     public string ReceiptId { get; set; }
//
//     [Parameter("address", "asset", 2, true)]
//     public string Asset { get; set; }
//
//     [Parameter("address", "owner", 3, true)]
//     public string Owner { get; set; }
//
//     [Parameter("uint256", "amount", 4, false)]
//     public BigInteger Amount { get; set; }
// }

[Event("Send")]
public class SendEventDTO : IEventDTO
{
    [Parameter("string", "requestId", 1, true)]
    public string RequestId { get; set; }

    [Parameter("address", "sender", 2, true)]
    public string Sender { get; set; }

    [Parameter("uint256", "targetChain", 3, true)]
    public BigInteger TargetChain { get; set; }

    [Parameter("address", "targetContract", 4, true)]
    public string Receiver { get; set; }

    [Parameter("bytes", "data", 5, false)] 
    public byte[] Data { get; set; }
    
    [Parameter("bytes", "tokenAmounts", 6, false)] 
    public byte[] TokenAmounts { get; set; }
}