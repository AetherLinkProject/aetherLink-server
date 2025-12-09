using AElf;
using AElf.Cryptography;
using AElf.Cryptography.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AetherLink.Multisignature.Tests;

[TestClass]
public class Tests
{
    [TestMethod]
    public void MultiSignatureTest()
    {
        int participant = 7;
        var t = participant / 2 + 1;

        var pubKeys = new string[participant];
        var secrets = new byte[participant][];
        byte[] msgByte = HashHelper.ComputeFrom("10").ToByteArray();
        for (int i = 0; i < participant; i++)
        {
            var kp = CryptoHelper.GenerateKeyPair();
            pubKeys[i] = kp.PublicKey.ToHex();
            secrets[i] = kp.PrivateKey;
        }

        var multiSignatures = new MultiSignature[participant];
        for (int i = 0; i < participant; i++)
        {
            multiSignatures[i] = new MultiSignature(secrets[i], msgByte, pubKeys, t);
        }

        var partialSigns = new PartialSignatureDto[participant];
        for (int i = 0; i < participant; i++)
        {
            partialSigns[i] = multiSignatures[i].GeneratePartialSignature();
        }

        for (int i = 0; i < participant; i++)
        {
            for (int j = 0; j < participant; j++)
            {
                if (i == j) continue;
                Assert.IsTrue(multiSignatures[i].ProcessPartialSignature(partialSigns[j]), $"multiSignature {i} partial {j} failure.");
            }
        }

        Assert.IsTrue(multiSignatures[0].IsEnoughPartialSig(), "not enough partial Signature.");
        Assert.IsTrue(multiSignatures[0].TryGetSignatures(out var signature), "Signature fail.");
    }

    [TestMethod]
    public void MultiSignature_generate_multiSignature_invalid_Test()
    {
        byte[] msgByte = HashHelper.ComputeFrom("10").ToByteArray();
        var kp = CryptoHelper.GenerateKeyPair();
        var pubKeys = new[] { kp.PublicKey.ToHex() };

        Assert.ThrowsException<InvalidPrivateKeyException>(
            () => new MultiSignature(new byte[] { }, msgByte, pubKeys, 1));

        Assert.ThrowsException<Exception>(() => new MultiSignature(kp.PrivateKey, msgByte, new string[] { }, 1));
    }


    [TestMethod]
    public void MultiSignature_process_partial_signature_fail_Test()
    {
        int participant = 2;
        var t = participant / 2 + 1;

        var pubKeys = new string[participant];
        var secrets = new byte[participant][];
        byte[] msgByte = HashHelper.ComputeFrom("10").ToByteArray();
        for (int i = 0; i < participant; i++)
        {
            var kp = CryptoHelper.GenerateKeyPair();
            pubKeys[i] = kp.PublicKey.ToHex();
            secrets[i] = kp.PrivateKey;
        }

        var multiSignatures = new MultiSignature[participant];
        for (int i = 0; i < participant; i++)
        {
            multiSignatures[i] = new MultiSignature(secrets[i], msgByte, pubKeys, t);
        }

        var partialSigns = new PartialSignatureDto[participant];
        for (int i = 0; i < participant; i++)
        {
            partialSigns[i] = multiSignatures[i].GeneratePartialSignature();
        }

        var rightI = partialSigns[1].Index;
        partialSigns[1].Index = 3;
        Assert.IsFalse(multiSignatures[0].ProcessPartialSignature(partialSigns[1]), "multiSignature partial failure.");
        partialSigns[1].Index = rightI;

        var rightSig = partialSigns[1].Signature;
        partialSigns[1].Signature = new byte[] { };
        Assert.IsFalse(multiSignatures[0].ProcessPartialSignature(partialSigns[1]), "multiSignature partial failure.");
        partialSigns[1].Signature = rightSig;

        Assert.IsTrue(multiSignatures[0].ProcessPartialSignature(partialSigns[1]), "multiSignature partial success.");
    }

    [TestMethod]
    public void MultiSignature_not_enough_partial_signature_Test()
    {
        int participant = 7;
        var t = participant / 2 + 1;

        var pubKeys = new string[participant];
        var secrets = new byte[participant][];
        byte[] msgByte = HashHelper.ComputeFrom("10").ToByteArray();
        for (int i = 0; i < participant; i++)
        {
            var kp = CryptoHelper.GenerateKeyPair();
            pubKeys[i] = kp.PublicKey.ToHex();
            secrets[i] = kp.PrivateKey;
        }

        var multiSignatures = new MultiSignature[participant];
        for (int i = 0; i < participant; i++)
        {
            multiSignatures[i] = new MultiSignature(secrets[i], msgByte, pubKeys, t);
        }

        var partialSigns = new PartialSignatureDto[participant];
        for (int i = 0; i < participant; i++)
        {
            partialSigns[i] = multiSignatures[i].GeneratePartialSignature();
        }

        Assert.IsFalse(multiSignatures[0].IsEnoughPartialSig(), "not enough partial Signature.");
        for (int j = 1; j < t - 1; j++)
        {
            Assert.IsTrue(multiSignatures[0].ProcessPartialSignature(partialSigns[j]), $"multiSignature partial {j} failure.");
        }

        Assert.IsFalse(multiSignatures[0].IsEnoughPartialSig(), "not enough partial Signature.");
        for (int j = t; j < participant; j++)
        {
            Assert.IsTrue(multiSignatures[0].ProcessPartialSignature(partialSigns[j]), $"multiSignature partial {j} failure.");
        }

        Assert.IsTrue(multiSignatures[0].IsEnoughPartialSig(), "not enough partial Signature.");
    }

    [TestMethod]
    public void MultiSignature_partial_signature_already_received_Test()
    {
        var kp = CryptoHelper.GenerateKeyPair();

        var multiSignature = new MultiSignature(kp.PrivateKey, HashHelper.ComputeFrom("10").ToByteArray(),
            new[] { kp.PublicKey.ToHex() }, 1);
        Assert.IsFalse(multiSignature.ProcessPartialSignature(multiSignature.GeneratePartialSignature()),
            "partial signature already received.");
    }
}