using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AElf;
using AElf.Cryptography;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using Google.Protobuf;
using Nethereum.ABI;
using Nethereum.Util;

namespace AetherLink.Worker.Core.Common;

public class EvmHelper
{
    public static byte[] OffChainSign(ReportContextDto context, CrossChainReportDto report, EvmOptions chainConfig)
    {
        var reportHash = GenerateReportHash(context, report);
        var privateKey = ByteArrayHelper.HexStringToByteArray(chainConfig.SignerSecret);
        return CryptoHelper.SignWithPrivateKey(privateKey, reportHash);
    }

    public static bool OffChainVerify(ReportContextDto context, int index, CrossChainReportDto report, byte[] sign,
        string[] distPublicKey)
    {
        if (sign.Length <= 0 || index < 0 || index > distPublicKey.Length) return false;
        var reportHash = GenerateReportHash(context, report);
        CryptoHelper.RecoverPublicKey(sign, reportHash, out var pubkey);
        return pubkey != null && distPublicKey[index] == pubkey.ToHex();
    }

    private static byte[] GenerateReportHash(ReportContextDto context, CrossChainReportDto report)
    {
        var reportContextDecoded = GenerateReportContextBytes(context);
        var message = GenerateMessageBytes(report.Message);
        var tokenTransferMetadataDecode = GenerateTokenTransferMetadataBytes(report.TokenTransferMetadataDto);
        return GenerateReportHash(reportContextDecoded, message, tokenTransferMetadataDecode);
    }

    public static ( byte[][], byte[][], byte[]) AggregateSignatures(List<byte[]> signatureByteList)
    {
        var signaturesCount = signatureByteList.Count;
        var r = new byte[signaturesCount][];
        var s = new byte[signaturesCount][];
        var v = new byte[32];
        var index = 0;
        foreach (var signatureBytes in signatureByteList)
        {
            r[index] = signatureBytes.Take(32).ToArray();
            s[index] = signatureBytes.Skip(32).Take(32).ToArray();
            v[index] = signatureBytes.Last();
            index++;
        }

        return (r, s, v);
    }

    private static byte[] GenerateReportHash(byte[] reportContext, byte[] message, byte[] tokenTransferMetadata)
    {
        var abiEncode = new ABIEncode();
        var result = abiEncode.GetABIEncoded(reportContext, message, tokenTransferMetadata);
        return Sha3Keccack.Current.CalculateHash(result);
    }

    public static byte[] GenerateReportContextBytes(ReportContextDto reportContext)
    {
        var abiEncode = new ABIEncode();
        var encoded = abiEncode.GetABIEncoded(
            new ABIValue("bytes32", ByteString.FromBase64(reportContext.MessageId).ToByteArray()),
            new ABIValue("uint256", reportContext.SourceChainId),
            new ABIValue("uint256", reportContext.TargetChainId),
            new ABIValue("string", reportContext.Sender),
            new ABIValue("address", ByteString.FromBase64(reportContext.Receiver).ToHex(true)));
        return encoded;
    }

    public static byte[] GenerateTokenTransferMetadataBytes(TokenTransferMetadataDto tokenTransferMetadata)
    {
        if (tokenAmount == null) return new byte[] { };

        var abiEncode = new ABIEncode();
        var encoded = abiEncode.GetABIEncoded(
            tokenTransferMetadata.ExtraData,
            (int)tokenTransferMetadata.TargetChainId,
            // TokenTransferMetadata.Receiver,
            tokenTransferMetadata.TokenAddress,
            tokenTransferMetadata.Symbol,
            (int)tokenTransferMetadata.Amount
        );
        return encoded;
    }

    public static byte[] GenerateMessageBytes(string message) => ByteString.FromBase64(message).ToByteArray();

    public static EvmOptions GetEvmContractConfig(long chainId, EvmContractsOptions options)
    {
        EvmOptions chainConfig = null;
        switch (chainId)
        {
            case ChainIdConstants.EVM:
                options.ContractConfig.TryGetValue("EVM", out chainConfig);
                break;
            case ChainIdConstants.BSC:
                options.ContractConfig.TryGetValue("BSC", out chainConfig);
                break;
            case ChainIdConstants.BSCTEST:
                options.ContractConfig.TryGetValue("BSCTEST", out chainConfig);
                break;
            case ChainIdConstants.SEPOLIA:
                options.ContractConfig.TryGetValue("SEPOLIA", out chainConfig);
                break;
        }

        return chainConfig;
    }
}