using System;
using System.Threading.Tasks;
using AElf;
using AElf.Client.Dto;
using AElf.Client.Service;
using AetherLink.AIServer.Core.ContractHandler;
using AetherLink.AIServer.Core.Dtos;
using AetherLink.AIServer.Core.Options;
using AetherLink.Contracts.AIFeeds;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AetherLink.AIServer.Core.Providers;

public interface ITransmitter
{
    Task SendTransmitTransactionAsync(OracleContext ctx, ByteString report, byte[] signature);
}

public class Transmitter : ITransmitter, ITransientDependency
{
    private readonly TransmitterOption _option;
    private readonly ILogger<Transmitter> _logger;
    private readonly IBlockchainClientFactory<AElfClient> _blockchainClientFactory;

    public Transmitter(IOptions<TransmitterOption> option, ILogger<Transmitter> logger,
        IBlockchainClientFactory<AElfClient> blockchainClientFactory)
    {
        _logger = logger;
        _option = option.Value;
        _blockchainClientFactory = blockchainClientFactory;
    }

    public async Task SendTransmitTransactionAsync(OracleContext ctx, ByteString report, byte[] signature)
    {
        var chainId = ChainHelper.ConvertChainIdToBase58(ctx.ChainId);
        var input = new AIRequestTransmitInput
        {
            OracleContext = ctx,
            Report = report,
            Signature = ByteStringHelper.FromHexString(signature.ToHex())
        };

        var transactionResult = await SendTransactionAsync(chainId, input);

        _logger.LogInformation($"Transmit transaction id: {transactionResult.TransactionId}");
    }

    private async Task<SendTransactionOutput> SendTransactionAsync(string chainId, IMessage param)
    {
        try
        {
            var client = _blockchainClientFactory.GetClient(chainId);
            var from = client.GetAddressFromPrivateKey(_option.PrivateKey);
            var to = _option.AIOracleContractAddress;
            var methodName = ContractConstants.AIRequestTransmit;
            var transaction = await client.GenerateTransactionAsync(from, to, methodName, param);
            var rawTx = client.SignTransaction(_option.PrivateKey, transaction).ToByteArray().ToHex();
            return await client.SendTransactionAsync(new SendTransactionInput { RawTransaction = rawTx });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "SendTransactionAsync failed");
            throw;
        }
    }
}