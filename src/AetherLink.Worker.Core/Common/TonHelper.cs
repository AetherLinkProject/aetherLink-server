using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Text.Unicode;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Exceptions;
using Google.Protobuf;
using GraphQL.Introspection;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Utilities.Encoders;
using Ramp;
using TonSdk.Contracts.Wallet;
using TonSdk.Core;
using TonSdk.Core.Boc;
using Virgil.Crypto;
using KeyPair = TonSdk.Core.Crypto.KeyPair;

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

    public static Cell BuildUnsignedCell(byte[] messageIdBytes, long sourceChainId, long targetChainId, byte[] sender,
        Address receiverAddress, byte[] message, TokenAmountDto tokenAmount)
    {
        var body = BuildMessageBody(sourceChainId, targetChainId, sender, receiverAddress, message, tokenAmount);
        var unsignCell = new CellBuilder()
            // .StoreInt(messageId, 256)
            .StoreBytes(Ensure128ByteArray(messageIdBytes))
            .StoreAddress(receiverAddress)
            .StoreRef(body)
            .Build();

        return unsignCell;
    }

    private static byte[] Ensure128ByteArray(byte[] bytes)
    {
        switch (bytes.Length)
        {
            case > 16:
                // Trim to 16 bytes
                return bytes.Take(16).ToArray();
            case < 16:
            {
                // Pad with leading zeros to 16 bytes
                var padded = new byte[16];
                Array.Copy(bytes, 0, padded, 16 - bytes.Length, bytes.Length);
                return padded;
            }
            default:
                // If already 16 bytes, return as-is
                return bytes;
        }
    }

    public static Cell BuildMessageBody(long sourceChainId, long targetChainId, byte[] sender, Address receiverAddress,
        byte[] message, TokenAmountDto tokenAmount = null)
    {
        return new CellBuilder()
            .StoreUInt((int)sourceChainId, 32)
            .StoreUInt((int)targetChainId, 32)
            .StoreRef(new CellBuilder().StoreBytes(sender).Build())
            .StoreRef(new CellBuilder().StoreAddress(receiverAddress).Build())
            .StoreRef(ConvertMessageBytesToCell(message))
            .StoreRef(BuildTokenAmountInfo(tokenAmount))
            .Build();
    }

    public static Address ConvertAddress(string receiver) => new(receiver);

    public static Cell ConvertMessageBytesToCell(byte[] message)
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

    private static Cell BuildTokenAmountInfo(TokenAmountDto tokenAmountDto = null)
    {
        var result = new CellBuilder();
        if (tokenAmountDto == null)
        {
            return result.Build();
        }

        result.StoreRef(new CellBuilder().StoreBytes(Base64.Decode(tokenAmountDto.SwapId)).Build());
        result.StoreUInt(tokenAmountDto.TargetChainId, 64);
        result.StoreRef(new CellBuilder().StoreBytes(Encoding.UTF8.GetBytes(tokenAmountDto.TargetContractAddress))
            .Build());
        result.StoreRef(new CellBuilder().StoreAddress(new Address(tokenAmountDto.TokenAddress)).Build());
        result.StoreRef(new CellBuilder().StoreBytes(Encoding.UTF8.GetBytes(tokenAmountDto.OriginToken)).Build());
        result.StoreUInt(tokenAmountDto.Amount, 256);

        return result.Build();
    }
}