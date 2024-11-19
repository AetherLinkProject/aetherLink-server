using System.Numerics;
using System.Text;
using System.Text.Unicode;
using AetherLink.Worker.Core.Dtos;
using Google.Protobuf;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Utilities.Encoders;
using Ramp;
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

    public static Cell BuildUnsignedCell(BigInteger messageId, long sourceChainId, long targetChainId, byte[] sender,
        Address receiverAddress, byte[] message, TokenAmountDto tokenAmount)
    {
        var body = BuildMessageBody(sourceChainId, targetChainId, sender, receiverAddress, message, tokenAmount);
        var unsignCell = new CellBuilder()
            .StoreInt(messageId, 256)
            .StoreAddress(receiverAddress)
            .StoreRef(body)
            .Build();

        return unsignCell;
    }

    public static Cell BuildMessageBody(long sourceChainId, long targetChainId, byte[] sender, Address receiverAddress,
        byte[] message, TokenAmountDto tokenAmount =null)
    {
        return new CellBuilder()
            .StoreUInt(sourceChainId, 64)
            .StoreUInt(targetChainId, 64)
            .StoreRef(new CellBuilder().StoreBytes(sender).Build())
            .StoreRef(new CellBuilder().StoreAddress(receiverAddress).Build())
            .StoreRef(new CellBuilder().StoreBytes(message).Build())
            .StoreRef(BuildTokenAmountInfo(tokenAmount))
            .Build();
    }

    public static Address ConvertAddress(string receiver) => new(ByteString.FromBase64(receiver).ToStringUtf8());

    private static Cell BuildTokenAmountInfo(TokenAmountDto tokenAmountDto = null)
    {
        var result = new CellBuilder();
        if (tokenAmountDto == null)
        {
            return result.Build();
        }

        result.StoreRef(new CellBuilder().StoreBytes( Encoding.UTF8.GetBytes(tokenAmountDto.SwapId)).Build());
        result.StoreUInt(tokenAmountDto.TargetChainId, 64);
        result.StoreRef(new CellBuilder().StoreBytes( Encoding.UTF8.GetBytes(tokenAmountDto.TargetContractAddress)).Build());
        result.StoreRef(new CellBuilder().StoreAddress(new Address(tokenAmountDto.TokenAddress)).Build());
        result.StoreRef(new CellBuilder().StoreBytes(Encoding.UTF8.GetBytes(tokenAmountDto.OriginToken)).Build());
        // result.StoreUInt(tokenAmountDto.Amount, 64);

        return result.Build();
    }
}