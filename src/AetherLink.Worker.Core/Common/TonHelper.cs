using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using AElf;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Exceptions;
using Google.Protobuf;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Utilities.Encoders;
using TonSdk.Contracts.Wallet;
using TonSdk.Core;
using TonSdk.Core.Boc;
using TonSdk.Core.Crypto;

namespace AetherLink.Worker.Core.Common;

public static class TonHelper
{
    public static Address GetAddressFromPrivateKey(string privateKey) => GetWalletFromPrivateKey(privateKey).Address;
    public static byte[] GetSecretKeyFromPrivateKey(string privateKey) => Hex.Decode(privateKey);

    public static WalletV4 GetWalletFromPrivateKey(string privateKey)
    {
        var secretKey = Hex.Decode(privateKey);
        var privateKeyParameters = new Ed25519PrivateKeyParameters(secretKey);
        var publicKey = privateKeyParameters.GeneratePublicKey();
        var keyPair = new KeyPair(secretKey, publicKey.GetEncoded());
        return new WalletV4(new() { PublicKey = keyPair.PublicKey });
    }

    public static HashmapE<int, byte[]> ConvertConsensusSignature(Dictionary<int, byte[]> consensusInfo)
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

        var hashmap = new HashmapE<int, byte[]>(new HashmapOptions<int, byte[]>
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

    public static CellBuilder PopulateMetadata(CellBuilder builder, ReportContextDto context, CrossChainDataDto meta)
        => PopulateMetadata(builder, context, meta.Message, meta.TokenTransferMetadata);

    public static CellBuilder PopulateMetadata(CellBuilder builder, ReportContextDto context, string message,
        TokenTransferMetadata tokenAmount)
    {
        var receiverAddress = new Address(context.Receiver);
        var sender = Base58CheckEncoding.Decode(context.Sender);
        var messageByte = Base64.Decode(message);
        var body = BuildMessageBody(
            context.SourceChainId,
            context.TargetChainId,
            sender,
            receiverAddress,
            messageByte,
            tokenAmount);
        var messageId = Ensure128ByteArray(context.MessageId);
        return builder.StoreInt(messageId, TonMetaDataConstants.MessageIdBitsSize)
            .StoreAddress(receiverAddress)
            .StoreRef(body);
    }

    public static bool VerifySignature(string publicKey, Cell metaData, byte[] signature)
    {
        var publicKeyParameters = new Ed25519PublicKeyParameters(Hex.Decode(publicKey));
        var signer = new Ed25519Signer();
        signer.Init(false, publicKeyParameters);
        var hash = metaData.Hash.ToBytes();
        signer.BlockUpdate(hash, 0, hash.Length);

        return signer.VerifySignature(signature);
    }

    private static BigInteger Ensure128ByteArray(string base64MessageId)
    {
        var messageIdBytes = ByteString.FromBase64(base64MessageId).ToByteArray();
        switch (messageIdBytes.Length)
        {
            case > 16:
                messageIdBytes = messageIdBytes.Take(16).ToArray();
                break;
            case < 16:
            {
                var paddedBytes = new byte[16];
                Array.Copy(messageIdBytes, 0, paddedBytes, 16 - messageIdBytes.Length, messageIdBytes.Length);
                messageIdBytes = paddedBytes;
                break;
            }
        }

        var reversedBytes = messageIdBytes.Reverse().ToArray();
        var bigInt = new BigInteger(reversedBytes, isBigEndian: true);

        return bigInt;
    }

    private static Cell BuildMessageBody(long sourceChainId, long targetChainId, byte[] sender, Address receiverAddress,
        byte[] message, TokenTransferMetadata tokenAmount = null)
    {
        return new CellBuilder()
            .StoreUInt((int)sourceChainId, TonMetaDataConstants.ChainIdIntSize)
            .StoreUInt((int)targetChainId, TonMetaDataConstants.ChainIdIntSize)
            .StoreRef(new CellBuilder().StoreBytes(sender).Build())
            .StoreRef(new CellBuilder().StoreAddress(receiverAddress).Build())
            .StoreRef(ConvertMessageBytesToCell(message))
            .StoreRef(BuildTokenAmountInfo(tokenAmount))
            .Build();
    }

    public static byte[] ConvertMessageCellToBytes(Cell messageCell)
    {
        var cellSlice = messageCell.Parse();
        var bitLength = cellSlice.LoadUInt(16);
        var result = cellSlice.LoadBits((int)bitLength).ToBytes();
        var refCount = (int)cellSlice.LoadUInt(8);
        if (refCount == 0)
        {
            return result;
        }

        if (refCount != 1)
        {
            throw new ProtocolException("Ton Protocol analysis message error");
        }

        var refBody = cellSlice.LoadRef();
        var refBytes = ConvertMessageCellToBytes(refBody);

        return result.Concat(refBytes).ToArray();
    }

    private static Cell ConvertMessageBytesToCell(byte[] message)
    {
        var totalCellCount = message.Length / TonEnvConstants.PerCellStorageBytesCount;
        if (message.Length % TonEnvConstants.PerCellStorageBytesCount > 0)
        {
            totalCellCount += 1;
        }

        var startIndex = 0;
        List<CellBuilder> builderList = new List<CellBuilder>();
        for (var i = 0; i < totalCellCount; i++)
        {
            var endIndex = startIndex + TonEnvConstants.PerCellStorageBytesCount;
            if (endIndex >= message.Length)
            {
                endIndex = message.Length;
            }

            var byteLength = endIndex - startIndex;
            byte[] tempBytes = new byte[byteLength];
            Array.Copy(message, startIndex, tempBytes, 0, byteLength);
            CellBuilder builder = new CellBuilder().StoreUInt(byteLength * 8, 16)
                .StoreBitsSlice(new BitsSlice(new Bits(tempBytes)));

            builderList.Add(builder);

            startIndex = endIndex;
        }

        // last builder should not have extra ref
        builderList.Last().StoreInt(0, 8);
        if (builderList.Count == 1)
        {
            return builderList[0].Build();
        }

        var tempCellRef = builderList.Last();
        for (var i = builderList.Count - 2; i >= 0; i--)
        {
            var currentBuilder = builderList[i];
            currentBuilder.StoreUInt(1, 8).StoreRef(tempCellRef.Build());
            tempCellRef = currentBuilder;
        }

        return tempCellRef.Build();
    }

    private static Cell BuildTokenAmountInfo(TokenTransferMetadata tokenTransferMetadata = null)
    {
        var result = new CellBuilder();
        if (tokenTransferMetadata == null)
        {
            return result.Build();
        }

        result.StoreRef(new CellBuilder().StoreBytes(Base64.Decode(tokenTransferMetadata.ExtraData)).Build());
        result.StoreInt(tokenTransferMetadata.TargetChainId, TonMetaDataConstants.ChainIdIntSize);
        // result.StoreRef(new CellBuilder().StoreBytes(Encoding.UTF8.GetBytes(tokenTransferMetadata.Receiver))
        //     .Build());
        result.StoreRef(new CellBuilder().StoreAddress(new Address(tokenTransferMetadata.TokenAddress)).Build());
        result.StoreRef(new CellBuilder().StoreBytes(Encoding.UTF8.GetBytes(tokenTransferMetadata.Symbol)).Build());
        result.StoreUInt(tokenTransferMetadata.Amount, TonMetaDataConstants.AmountUIntSize);

        return result.Build();
    }
}