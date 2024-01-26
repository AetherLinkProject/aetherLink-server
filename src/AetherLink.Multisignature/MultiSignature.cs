using AElf;
using AElf.Cryptography;
using Google.Protobuf;

namespace AetherLink.Multisignature;

public class MultiSignature
{
    private readonly HashSet<int> _partialSignatureIndex;
    private readonly List<byte[]> _partialSignatures;
    private readonly byte[] _secret;
    private readonly byte[] _msgByte;
    private readonly string[] _publicKeys;
    private bool _signed;
    private readonly int _threshold;
    private readonly int _selfIndex;

    public MultiSignature(byte[] secret, byte[] msgByte, string[] publicKeys, int threshold)
    {
        _partialSignatures = new List<byte[]>();
        _partialSignatureIndex = new HashSet<int>();
        _secret = secret;
        _msgByte = msgByte;
        _publicKeys = publicKeys;
        _threshold = threshold;
        if (!TryGetPubkeyIndex(out _selfIndex))
            throw new Exception("MultiSignature: public key not found in list of participants");
    }

    public PartialSignatureDto GeneratePartialSignature()
    {
        var sig = CryptoHelper.SignWithPrivateKey(ByteArrayHelper.HexStringToByteArray(_secret.ToHex()), _msgByte);
        if (!_signed)
        {
            _partialSignatureIndex.Add(_selfIndex);
            _partialSignatures.Add(sig);
            _signed = true;
        }

        return new PartialSignatureDto
        {
            Signature = sig,
            Index = _selfIndex
        };
    }

    // METHOD ProcessPartialSignature: Please pay attention to concurrency processing
    public bool ProcessPartialSignature(PartialSignatureDto sign)
    {
        if (_partialSignatureIndex.Contains(sign.Index)) return false;
        if (sign.Index > _publicKeys.Length) return false;

        CryptoHelper.RecoverPublicKey(sign.Signature, _msgByte, out var pubkey);
        if (pubkey == null || _publicKeys[sign.Index] != pubkey.ToHex()) return false;
        _partialSignatureIndex.Add(sign.Index);
        _partialSignatures.Add(sign.Signature);
        return true;
    }

    public bool IsEnoughPartialSig() => _partialSignatures.Count >= _threshold;

    public bool TryGetSignatures(out List<ByteString> signature)
    {
        signature = new List<ByteString>();
        if (!IsEnoughPartialSig()) return false;
        signature = _partialSignatures.Select(sig => ByteStringHelper.FromHexString(sig.ToHex())).ToList();
        return true;
    }

    private bool TryGetPubkeyIndex(out int index)
    {
        index = 0;
        var pubkey = CryptoHelper.FromPrivateKey(_secret).PublicKey.ToHex();
        for (var i = 0; i < _publicKeys.Length; i++)
        {
            if (_publicKeys[i] != pubkey) continue;
            index = i;
            return true;
        }

        return false;
    }
}