using System.Threading.Tasks;
using AetherLink.Worker.Core.Constants;
using Volo.Abp.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Numerics;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider.TonIndexer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Utilities.Encoders;
using TonSdk.Contracts.Wallet;
using TonSdk.Core;
using TonSdk.Core.Block;
using TonSdk.Core.Boc;

namespace AetherLink.Worker.Core.ChainHandler;

public class TonChainWriter : ChainWriter
{
    public override long ChainId => ChainIdConstants.TON;

    private readonly WalletV4 _wallet;
    private readonly ILogger<TonChainWriter> _logger;
    private readonly TonIndexerRouter _indexerRouter;
    private readonly TonPrivateOptions _privateOptions;
    private readonly TonPublicOptions _tonPublicOptions;

    public TonChainWriter(TonIndexerRouter indexerRouter, ILogger<TonChainWriter> logger,
        IOptionsSnapshot<TonPublicOptions> tonPublicOptions, IOptionsSnapshot<TonPrivateOptions> privateOptions)
    {
        _logger = logger;
        _indexerRouter = indexerRouter;
        _privateOptions = privateOptions.Value;
        _tonPublicOptions = tonPublicOptions.Value;
        _wallet = TonHelper.GetWalletFromPrivateKey(_privateOptions.TransmitterSecretKey);
    }

    public override async Task<string> SendCommitTransactionAsync(ReportContextDto reportContext,
        Dictionary<int, byte[]> signatures, CrossChainDataDto crossChainData)
    {
        var receiverAddress = TonHelper.ConvertAddress(reportContext.Receiver);
        var seqno = await _indexerRouter.GetAddressSeqno(
            TonHelper.GetAddressFromPrivateKey(_privateOptions.TransmitterSecretKey));
        if (seqno == null)
        {
            _logger.LogError($"[TonHelper] get seqno error, messageId is {reportContext.MessageId}");
            return null;
        }

        var bodyCell = new CellBuilder().StoreUInt(TonOpCodeConstants.ForwardTx, 32)
            .StoreInt(new BigInteger(new ReadOnlySpan<byte>
                (Base64.Decode(reportContext.MessageId)), false, true), 256)
            .StoreAddress(receiverAddress)
            .StoreRef(TonHelper.BuildMessageBody(reportContext.SourceChainId,
                reportContext.TargetChainId, Base64.Decode(reportContext.Sender), receiverAddress,
                Base64.Decode(crossChainData.Message), crossChainData.TokenAmount))
            .StoreRef(new CellBuilder().StoreDict(ConvertConsensusSignature(signatures)).Build())
            .Build();

        var msg = _wallet.CreateTransferMessage(new[]
        {
            new WalletTransfer
            {
                Message = new InternalMessage(new()
                {
                    Info = new(new()
                    {
                        Src = _wallet.Address,
                        Dest = new(_tonPublicOptions.ContractAddress),
                        Value = new(_privateOptions.TransmitterFee)
                    }),
                    Body = bodyCell,
                    StateInit = null,
                }),
                Mode = 0 // message mode
            }
        }, (uint)seqno).Sign(TonHelper.GetSecretKeyFromPrivateKey(_privateOptions.TransmitterSecretKey));

        _logger.LogDebug(
            $"[TonChainWriter] Ready to commit , sourceChainId:{reportContext.SourceChainId} targetChainId:{reportContext.TargetChainId} messageId:{reportContext.MessageId}");

        return await _indexerRouter.CommitTransaction(msg.Cell);
    }

    private HashmapE<int, byte[]> ConvertConsensusSignature(Dictionary<int, byte[]> consensusInfo)
    {
        var hashMapSerializer = new HashmapSerializers<int, byte[]>
        {
            Key = (key) =>
            {
                var cell = new CellBuilder().StoreInt(new BigInteger(key), 256).Build();
                return cell.Parse().LoadBits(256);
            },

            Value = (value) => new CellBuilder().StoreRef(new CellBuilder().StoreBytes(value).Build()).Build()
        };

        var hashmap = new HashmapE<int, byte[]>(new HashmapOptions<int, byte[]>()
        {
            KeySize = 256,
            Serializers = hashMapSerializer,
        });

        foreach (var (nodeIndex, signature) in consensusInfo)
        {
            hashmap.Set(nodeIndex, signature);
        }

        return hashmap;
    }
}

public class TonChainReader : ChainReader, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.TON;

    public override Task<byte[]> CallTransactionAsync(byte[] transaction)
    {
        throw new System.NotImplementedException();
    }

    public override async Task<TransactionResultDto> GetTransactionResultAsync(string transactionId)
    {
        // todo: use official full node to check transaction result 
        return new() { State = TransactionState.Success };
    }

    public override string ConvertBytesToAddressStr(byte[] addressBytes)
    {
        throw new System.NotImplementedException();
    }
}