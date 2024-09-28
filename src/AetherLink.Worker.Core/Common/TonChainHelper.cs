using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common.TonIndexer;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Utilities.Encoders;
using TonSdk.Contracts.Wallet;
using TonSdk.Core;
using TonSdk.Core.Block;
using TonSdk.Core.Boc;
using TonSdk.Core.Crypto;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Common;

public sealed partial class TonHelper: ISingletonDependency
{
    private readonly TonPublicConfigOptions _tonPublicConfigOptions;
    private KeyPair _keyPair ;
    private readonly ILogger<TonHelper> _logger;
    private readonly TonIndexerRouter _indexerRouter;
    private readonly string _transmitterFee;
    
    public TonHelper(IOptionsSnapshot<TonPublicConfigOptions> tonPublicOptions,TonIndexerRouter indexerRouter, IOptionsSnapshot<TonSecretConfigOptions> tonSecretOptions, ILogger<TonHelper> logger, IStorageProvider storageProvider)
    {
        _tonPublicConfigOptions = tonPublicOptions.Value;
        _transmitterFee = tonSecretOptions.Value.TransmitterFee;
        _storageProvider = storageProvider;
        _indexerRouter = indexerRouter;
        
        var secretKey = Hex.Decode(tonSecretOptions.Value.TransmitterSecretKey);
        var publicKey = Hex.Decode(tonSecretOptions.Value.TransmitterPublicKey);
        
        _keyPair = new KeyPair(secretKey, publicKey);

        _logger = logger;
    }

    [ItemCanBeNull]
    public async Task<string> SendTransaction(CrossChainForwardMessageDto crossChainForwardMessageDto , Dictionary<int, byte[]> consensusInfo)
    {
        var walletV4 = new WalletV4(new WalletV4Options(){PublicKey = _keyPair.PublicKey});
        
        var receiverAddress = crossChainForwardMessageDto.Receiver;
        
        var seqno = await _indexerRouter.GetAddressSeqno(walletV4.Address);
        if (seqno == null)
        {
            _logger.LogError($"[Send Ton Transaction] get seqno error, messageId is {crossChainForwardMessageDto.MessageId}");
            return null;
        }
        
        var bodyCell = new CellBuilder()
            .StoreUInt(TonOpCodeConstants.ForwardTx, 32)
            .StoreInt(new BigInteger(Base64.Decode(crossChainForwardMessageDto.MessageId)),256)
            .StoreAddress(new Address(receiverAddress))
            .StoreRef(BuildMessageBody(crossChainForwardMessageDto.SourceChainId, 
                    crossChainForwardMessageDto.TargetChainId, 
                Base64.Decode(crossChainForwardMessageDto.Sender),
                      new Address(receiverAddress),
                Base64.Decode(crossChainForwardMessageDto.Message)))
            .StoreRef(new CellBuilder().StoreDict(ConvertConsensusSignature(consensusInfo)).Build())
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
        if (resendType != TonResendTypeConstants.IntervalSeconds) return result;
        var intervalSeconds = bodySlice.LoadUInt(32);

        result.ResendTime = (long)intervalSeconds + tonTransactionDto.BlockTime;
            
        // next check tx time
        result.CheckCommitTime = (long)intervalSeconds + tonTransactionDto.BlockTime +
                                 TonEnvConstants.ResendMaxWaitSeconds;

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

    public bool CheckSign(CrossChainForwardMessageDto crossChainForwardMessageDto, byte[] sign, int nodeIndex)
    {
        var bodyCell = BuildUnsignedCell(new BigInteger(Base64.Decode(crossChainForwardMessageDto.MessageId)),
            crossChainForwardMessageDto.SourceChainId,
            crossChainForwardMessageDto.TargetChainId,
            Base64.Decode(crossChainForwardMessageDto.Sender),
            new Address(crossChainForwardMessageDto.Receiver),
            Base64.Decode(crossChainForwardMessageDto.Message));

        var nodeInfo = _tonPublicConfigOptions.OracleNodeInfoList.Find(f => f.Index == nodeIndex);
        if (nodeInfo == null)
        {
            return false;
        }

        Ed25519PublicKeyParameters publicKeyParameters = new Ed25519PublicKeyParameters(Hex.Decode(nodeInfo.PublicKey));
        Ed25519Signer signer = new Ed25519Signer();
        signer.Init(false, publicKeyParameters);
        var hash = bodyCell.Hash.ToBytes();
        signer.BlockUpdate(hash, 0, hash.Length);

        return signer.VerifySignature(sign);
    }
    
    public byte[] ConsensusSign(string messageId, Int64 sourceChainId, Int64 targetChainId, byte[] sender, string receiverAddress, byte[] message)
    {
        var unsignCell = BuildUnsignedCell(new BigInteger(Base64.Decode(messageId)), sourceChainId, targetChainId, sender, new Address(receiverAddress), message);

        return KeyPair.Sign(unsignCell, this._keyPair.PrivateKey);
    }

    public async Task TestSendTransaction()
    {
        var messageId = new CellBuilder().StoreInt(234, 256).Build().Hash.ToBytes();
        var sourceId = 1;
        var targetId = 2;
        var sender = new byte[] { 1};
        var recieve = "EQBY0bXyWw0xZDy28TkYk93CKvKFxG4nlEgFAANThjvOrDtl";
        var message = new byte[] { 2};
        var leaderKeyPair = _keyPair;
        var signerLeader = ConsensusSign(Base64.ToBase64String(messageId), sourceId, targetId, sender,
            recieve, message);

        _keyPair = new KeyPair(Hex.Decode("d11abbb3c97ed14d86ef9b9eafc3d0395a12079755e936501dcfb9edb2e53184"),
            Hex.Decode("63cc96a52e34cb95257967b10e7ba03dc3a0fac8fa62dc96e5000c27f9fa3224"));
        var signerFollower = ConsensusSign(Base64.ToBase64String(messageId), sourceId, targetId, sender,
            recieve, message);
        
        _keyPair = leaderKeyPair;
        var consensusSign = new Dictionary<int,byte[]> ();
        consensusSign[0] = signerLeader;
        consensusSign[1] = signerFollower;

        var messageDto = new CrossChainForwardMessageDto
        {
            MessageId = Base64.ToBase64String(messageId),
            SourceChainId = sourceId,
            TargetChainId = targetId,
            TargetContractAddress = "EQBY0bXyWw0xZDy28TkYk93CKvKFxG4nlEgFAANThjvOrDtl",
            Sender = Base64.ToBase64String(sender),
            Receiver = recieve,
            Message = Base64.ToBase64String(message)
        };

        await SendTransaction(messageDto, consensusSign);
    }

    private Cell BuildUnsignedCell(BigInteger messageId, Int64 sourceChainId, Int64 targetChainId, byte[] sender,
        Address receiverAddress, byte[] message)
    {
        var body = BuildMessageBody(sourceChainId, targetChainId, sender, receiverAddress, message);
        var unsignCell = new CellBuilder()
            .StoreInt(messageId, 256)
            .StoreAddress(receiverAddress)
            .StoreRef(body)
            .Build();

        return unsignCell;
    }
    
    private Cell BuildMessageBody(Int64 sourceChainId, Int64 targetChainId, byte[] sender, Address receiverAddress,
        byte[] message)
    {
        return new CellBuilder()
            .StoreUInt(sourceChainId, 64)
            .StoreUInt(targetChainId, 64)
            .StoreRef(new CellBuilder().StoreBytes(sender).Build())
            .StoreRef(new CellBuilder().StoreAddress(receiverAddress).Build())
            .StoreRef(new CellBuilder().StoreBytes(message).Build())
            .Build();
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