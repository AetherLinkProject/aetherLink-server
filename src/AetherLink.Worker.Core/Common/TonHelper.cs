using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common.TonIndexer;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Worker;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUglify.Helpers;
using Org.BouncyCastle.Utilities.Encoders;
using TonSdk.Client;
using TonSdk.Core;
using TonSdk.Core.Boc;
using TonSdk.Core.Crypto;
using TonSdk.Contracts.Wallet;
using TonSdk.Core.Block;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Common;

public sealed class TonHelper: ISingletonDependency
{
    private readonly TonPublicConfigOptions _tonPublicConfigOptions;
    private readonly KeyPair _keyPair ;
    private readonly string _transmitterFee;
    private readonly ILogger<TonHelper> _logger;
    private readonly IStorageProvider _storageProvider;
    private readonly TonIndexerRouter _indexerRouter;
    private const string TonIndexerStorageKey = "TonIndexer";
    
    public TonHelper(IOptionsSnapshot<TonPublicConfigOptions> tonPublicOptions, IServiceProvider serviceProvider, IOptionsSnapshot<TonSecretConfigOptions> tonSecretOptions, ILogger<TonHelper> logger, IStorageProvider storageProvider)
    {
        _tonPublicConfigOptions = tonPublicOptions.Value;
        _transmitterFee = tonSecretOptions.Value.TransmitterFee;
        _storageProvider = storageProvider;
        _indexerRouter = serviceProvider.GetRequiredService<TonIndexerRouter>();
        
        var secretKey = Hex.Decode(tonSecretOptions.Value.TransmitterSecretKey);
        var publicKey = Hex.Decode(tonSecretOptions.Value.TransmitterPublicKey);
        
        _keyPair = new KeyPair(secretKey, publicKey);

        _logger = logger;
    }

    public string TonOracleContractAddress => _tonPublicConfigOptions.ContractAddress;
    
    [ItemCanBeNull]
    public async Task<string> SendTransaction(CrossChainForwardMessageDto crossChainForwardMessageDto , Dictionary<int, byte[]> consensusInfo)
    {
        var walletV4 = new WalletV4(new WalletV4Options(){PublicKey = _keyPair.PublicKey});
        
        var receiverAddress = Encoding.UTF8.GetString(Base64.Decode(crossChainForwardMessageDto.Receiver));
        
        var seqno = await _indexerRouter.GetAddressSeqno(walletV4.Address);
        if (seqno == null)
        {
            _logger.LogError($"[Send Ton Transaction] get seqno error, messageId is {crossChainForwardMessageDto.MessageId}");
            return null;
        }
        
        var bodyCell = new CellBuilder()
            .StoreUInt(TonOpCodeConstants.ForwardTx, 32)
            .StoreBytes(Base64.Decode(crossChainForwardMessageDto.MessageId))
            .StoreAddress(new Address(receiverAddress))
            .StoreRef(BuildMessageBody( crossChainForwardMessageDto.SourceChainId, 
                    crossChainForwardMessageDto.TargetChainId, 
                Base64.Decode(crossChainForwardMessageDto.Sender),
                      receiverAddress,
                Base64.Decode(crossChainForwardMessageDto.Message)))
            .StoreRef(new CellBuilder().StoreDict(BuildConsensusSignInfo(consensusInfo)).Build())
            .Build();
            
        var msg = walletV4.CreateTransferMessage(new[]
        {
            new WalletTransfer
            {
                Message = new InternalMessage(new InternalMessageOptions
                {
                    Info = new IntMsgInfo(new IntMsgInfoOptions
                    {
                        Src = walletV4.Address,
                        Dest = new Address(_tonPublicConfigOptions.ContractAddress),
                        Value = new Coins(_transmitterFee)
                    }),
                    Body = bodyCell,
                    StateInit = null,
                }),
                Mode = 0 // message mode
            }
        }, seqno ?? 0).Sign(_keyPair.PrivateKey);
            
        var result = await _indexerRouter.CommitTransaction(msg.Cell);
        if (result == null)
        {
            _logger.LogError($"[Send Ton Transaction] send transaction error,messageId is {crossChainForwardMessageDto.MessageId}");
            return null;
        }
        
        _logger.LogInformation($"[Send Ton Transaction] Cross to Ton: sourceChainId:{crossChainForwardMessageDto.SourceChainId} targetChainId:{crossChainForwardMessageDto.TargetChainId} messageId:{crossChainForwardMessageDto.Message} Transaction hash:{result}");
        
        return result;
    }

    public async Task<TonIndexerDto> GetTonIndexerFromStorage()
    {
        var result = await _storageProvider.GetAsync<TonIndexerDto>(TonIndexerStorageKey);
        if (result == null)
        {
            result = new TonIndexerDto();
        }

        return result;
    }

    public async Task StorageTonIndexer(TonIndexerDto tonIndexer)
    {
        await _storageProvider.SetAsync(TonIndexerStorageKey, tonIndexer);
    }
    
    public CrossChainForwardResendDto AnalysisResendTransaction(CrossChainToTonTransactionDto tonTransactionDto)
    {
        var body = Cell.From(tonTransactionDto.Body);
        var bodySlice = body.Parse();
        var opCode = bodySlice.LoadInt(32);
        if (opCode != TonOpCodeConstants.ResendTx)
        {
            _logger.LogError("AnalysisReceiveTransaction OpCode Error");
            return null;
        }

        var messageId = bodySlice.LoadBytes(64);
        var messageIdStr = Base64.ToBase64String(messageId);

        var result = new CrossChainForwardResendDto
        {
            MessageId = messageIdStr,
            TargetBlockHeight = tonTransactionDto.SeqNo,
            Hash = tonTransactionDto.Hash,
            TargetBlockGeneratorTime = tonTransactionDto.BlockTime,
            Status = ResendStatus.WaitConsensus
        };

        var resendType = bodySlice.LoadUInt(8);
        if (resendType == TonResendTypeConstants.IntervalSeconds)
        {
            var intervalSeconds = bodySlice.LoadUInt(32);
            
            // next check tx time
            result.CheckCommitTime = (long)intervalSeconds + tonTransactionDto.BlockTime +
                                     TonEnvConstants.ResendMaxWaitSeconds;
        }

        return result;
    }
    
    public CrossChainForwardMessageDto AnalysisForwardTransaction(CrossChainToTonTransactionDto tonTransactionDto)
    {
        var inMessageBody = Cell.From(tonTransactionDto.Body);
        var inMessageBodySlice = inMessageBody.Parse();
        var opCode = inMessageBodySlice.LoadUInt(32);
        if (opCode != TonOpCodeConstants.ForwardTx)
        {
            _logger.LogError("AnalysisForwardTransaction OpCode Error");
            return null;
        }
        var messageId = inMessageBodySlice.LoadBytes(64);
        var messageIdStr = Base64.ToBase64String(messageId);
        var targetAddr = inMessageBodySlice.LoadAddress();
        var targetAddrStr = targetAddr?.ToString();

        var proxyBody = inMessageBodySlice.LoadRef();
        var proxyBodySlice = proxyBody.Parse();

        var sourceChainId = proxyBodySlice.LoadUInt(64);
        var targetChainId = proxyBodySlice.LoadUInt(64);
        var sender = proxyBodySlice.LoadRef();
        var senderStr = Base64.ToBase64String(sender.Parse().Bits.ToBytes());
        var receive = proxyBodySlice.LoadRef();
        var receiveStr = Base64.ToBase64String(receive.Parse().Bits.ToBytes());
        var proxyMessage = proxyBodySlice.LoadRef();
        var proxyMessageStr = Base64.ToBase64String(proxyMessage.Parse().Bits.ToBytes());

        return new CrossChainForwardMessageDto
        {
            MessageId = messageIdStr,
            SourceChainId = (Int64) sourceChainId,
            TargetChainId = (Int64) targetChainId,
            TargetContractAddress = targetAddrStr,
            Sender = senderStr,
            Receiver = receiveStr,
            Message = proxyMessageStr
        };
    }
    
    public byte[] ConsensusSign(byte[] messageId, Int64 sourceChainId, Int64 targetChainId, byte[] sender, string receiverAddress, byte[] message)
    {
        var body = BuildMessageBody(sourceChainId, targetChainId, sender, receiverAddress, message);
        
        var unsignCell = new CellBuilder()
            .StoreBytes(messageId)
            .StoreAddress(new Address(receiverAddress))
            .StoreRef(body)
            .Build();

        return KeyPair.Sign(unsignCell, this._keyPair.PrivateKey);
    }

    private Cell BuildMessageBody(Int64 sourceChainId, Int64 targetChainId, byte[] sender, string receiverAddress,
        byte[] message)
    {
        
        return new CellBuilder()
            .StoreUInt(sourceChainId, 64)
            .StoreUInt(targetChainId, 64)
            .StoreRef(new CellBuilder().StoreBytes(sender).Build())
            .StoreRef(new CellBuilder().StoreAddress(new Address(receiverAddress)).Build())
            .StoreRef(new CellBuilder().StoreBytes(message).Build())
            .Build();
    }

    private HashmapE<int, byte[]> BuildConsensusSignInfo(Dictionary<int, byte[]> consensusInfo)
    {
        var hashMapSerializer = new HashmapSerializers<int, byte[]>
        {
            Key = (key) =>
            {
                var cell = new CellBuilder().StoreInt(new BigInteger(key), 256).Build();
                return new Bits(cell.Parse().LoadBytes(32));
            },
        
            Value = (value) => new CellBuilder().StoreBytes(value).Build()
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