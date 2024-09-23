using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using AElf.Client.Dto;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Utilities.Encoders;
using TonSdk.Client;
using TonSdk.Core;
using TonSdk.Core.Boc;
using TonSdk.Core.Crypto;
using TonSdk.Client.Stack;
using TonSdk.Contracts.Wallet;
using TonSdk.Core.Block;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Common;

public  class TonHelper: ISingletonDependency
{
    private readonly TonConfigOptions _tonConfigOptions;
    private readonly TonSdk.Core.Crypto.KeyPair _keyPair ;
    private readonly string _transmitterFee;
    private readonly ILogger<TonHelper> _logger;
    
    public TonHelper(IOptionsSnapshot<IConfiguration> snapshotConfig, ILogger<TonHelper> logger)
    {
        _tonConfigOptions = snapshotConfig.Value.GetSection("Chains:ChainInfos:Ton").Get<TonConfigOptions>();
        var secretKeyStr = snapshotConfig.Value.GetSection("OracleChainInfo:Ton:TransmitterSecretKey").Value;
        var publicKeyStr = snapshotConfig.Value.GetSection("OracleChainInfo:Ton:TransmitterPublicKey").Value;
        _transmitterFee = snapshotConfig.Value.GetSection("OracleChainInfo:Ton:TransmitterFee").Value;
        
        var secretKey = Hex.Decode(secretKeyStr);
        var publicKey = Hex.Decode(publicKeyStr);
        
        _keyPair = new KeyPair(secretKey, publicKey);

        _logger = logger;
    }

    public string TonOracleContractAddress => _tonConfigOptions.ContractAddress;
    
    [ItemCanBeNull]
    public async Task<string> SendTransaction(CrossChainForwardMessageDto crossChainForwardMessageDto , Dictionary<int, byte[]> consensusInfo)
    {
        var walletV4 = new WalletV4(new WalletV4Options(){PublicKey = _keyPair.PrivateKey});
        
        HttpParameters tonClientParams = new HttpParameters 
        {
            Endpoint = _tonConfigOptions.BaseUrl,
            ApiKey = string.IsNullOrEmpty(_tonConfigOptions.ApiKey)? null: _tonConfigOptions.ApiKey
        };

        using var tonClient = new TonClient(TonClientType.HTTP_TONCENTERAPIV2, tonClientParams);
        var receiverAddress = Encoding.UTF8.GetString(Base64.Decode(crossChainForwardMessageDto.Receiver));
        
        var seqno = await tonClient.Wallet.GetSeqno(walletV4.Address);
        var bodyCell = new CellBuilder()
            .StoreUInt(TonOpCodeConstants.ForwardTx, 32)
            .StoreBytes(Base64.Decode(crossChainForwardMessageDto.MessageId))
            .StoreAddress(new Address(receiverAddress))
            .StoreRef(BuildMessageBody( crossChainForwardMessageDto.SourceChainId, 
                    crossChainForwardMessageDto.TargetChainId, 
                Base64.Decode(crossChainForwardMessageDto.Sender),
                      receiverAddress,
                Base64.Decode(crossChainForwardMessageDto.Message)))
            .StoreRef(new CellBuilder().StoreDict<int, byte[]>(BuildConsensusSignInfo(consensusInfo)).Build())
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
                        Dest = new Address(_tonConfigOptions.ContractAddress),
                        Value = new Coins(_transmitterFee)
                    }),
                    Body = bodyCell,
                    StateInit = null,
                }),
                Mode = 0 // message mode
            }
        }, seqno ?? 0).Sign(_keyPair.PrivateKey);
            
        var result = await tonClient.SendBoc(msg.Cell);
        if (result != null)
        {
            _logger.LogInformation($"Cross to Ton: sourceChainId:{crossChainForwardMessageDto.SourceChainId} targetChainId:{crossChainForwardMessageDto.TargetChainId} messageId:{crossChainForwardMessageDto.Message} Transaction hash:{result.Value.Hash}");
        }
        
        return result?.Hash;
    }

    public CrossChainToTonTransactionDto AnalysisForwardTransaction(string inMessageBodyStr)
    {
        
    }
    
    public 
    
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