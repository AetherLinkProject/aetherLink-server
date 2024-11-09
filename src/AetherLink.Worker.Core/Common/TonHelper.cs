using System.Numerics;
using Org.BouncyCastle.Crypto.Parameters;
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

    public static Cell BuildUnsignedCell(BigInteger messageId, long sourceChainId, long targetChainId, byte[] sender,
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

    public static Cell BuildMessageBody(long sourceChainId, long targetChainId, byte[] sender, Address receiverAddress,
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
}