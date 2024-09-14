using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using AElf.Client.Dto;
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
    private readonly ILogger<TonHelper> _logger;
    private const int _operateCode = 3;
    
    public TonHelper(IOptionsSnapshot<IConfiguration> snapshotConfig, ILogger<TonHelper> logger)
    {
        _tonConfigOptions = snapshotConfig.Value.GetSection("Chains:ChainInfos:Ton").Get<TonConfigOptions>();
        var secretKeyStr = snapshotConfig.Value.GetSection("OracleChainInfo:Ton:TransmitterSecretKey").Value;
        var publicKeyStr = snapshotConfig.Value.GetSection("OracleChainInfo:Ton:TransmitterPublicKey").Value;
        
        var secretKey = Hex.Decode(secretKeyStr);
        var publicKey = Hex.Decode(publicKeyStr);
        
        _keyPair = new KeyPair(secretKey, publicKey);

        _logger = logger;
    }
    
    public async Task<string> SendTransaction(string businessContractAddress, byte[] transmissionData, Dictionary<int, byte[]> consensusInfo)
    {
        var walletV4 = new WalletV4(new WalletV4Options(){PublicKey = _keyPair.PrivateKey});
        
        HttpParameters tonClientParams = new HttpParameters 
        {
            Endpoint = _tonConfigOptions.BaseUrl,
            ApiKey = string.IsNullOrEmpty(_tonConfigOptions.ApiKey)? null: _tonConfigOptions.ApiKey
        };

        using var tonClient = new TonClient(TonClientType.HTTP_TONCENTERAPIV2, tonClientParams);
        
        var seqno = await tonClient.Wallet.GetSeqno(walletV4.Address);
        var bodyCell = new CellBuilder()
            .StoreUInt(_operateCode, 32)
            .StoreAddress(new Address(businessContractAddress))
            .StoreRef(new CellBuilder().StoreBytes(transmissionData).Build())
            .StoreRef(
                new CellBuilder().StoreBytes(ConsensusSign(businessContractAddress, transmissionData)).Build())
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
                        Value = new Coins(0.015)
                    }),
                    Body = bodyCell,
                    StateInit = null,
                }),
                Mode = 0 // message mode
            }
        }, seqno ?? 0).Sign(_keyPair.PrivateKey);
            
        var result = await tonClient.SendBoc(msg.Cell);
        _logger.LogInformation("");
        
        return result.Value.Hash;
    }
    
    public byte[] ConsensusSign(string tonContractAddress, byte[] transmissionData)
    {
        var cellBuilder = new CellBuilder().StoreBytes(transmissionData).Build();
        
        var builder = new CellBuilder();
        var unsignCell = builder.StoreInt(_operateCode, 32) // operate code
            .StoreAddress(new Address(tonContractAddress))
            .StoreCellSlice(new CellSlice(cellBuilder))
            .Build();

        return KeyPair.Sign(unsignCell, this._keyPair.PrivateKey);
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