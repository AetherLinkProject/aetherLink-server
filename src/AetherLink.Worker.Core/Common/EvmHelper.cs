using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AElf;
using AElf.Cryptography;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using Google.Protobuf;
using Nethereum.ABI;
using Nethereum.Util;

namespace AetherLink.Worker.Core.Common;

public class EvmHelper
{
    public static byte[] OffChainSign(ReportContextDto context, CrossChainReportDto report, ChainConfig chainConfig)
    {
        var reportContextDecoded = GenerateReportContextBytes(context);
        var message = GenerateMessageBytes(report.Message);
        var tokenAmountDecode = GenerateTokenAmountBytes(report.TokenAmount);
        var reportHash = GenerateReportHash(reportContextDecoded, message, tokenAmountDecode);
        var privateKey = ByteArrayHelper.HexStringToByteArray(chainConfig.SignerSecret);
        return CryptoHelper.SignWithPrivateKey(privateKey, reportHash);
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

    private static byte[] GenerateReportHash(byte[] reportContext, byte[] message, byte[] tokenAmount)
    {
        var abiEncode = new ABIEncode();
        var result = abiEncode.GetABIEncoded(
            reportContext,
            message,
            tokenAmount
        );
        return Sha3Keccack.Current.CalculateHash(result);
    }

    public static byte[] GenerateReportContextBytes(ReportContextDto reportContext)
    {
        var abiEncode = new ABIEncode();
        var encoded = abiEncode.GetABIEncoded(
            ByteString.FromBase64(reportContext.MessageId).ToByteArray(),
            (int)reportContext.SourceChainId,
            (int)reportContext.TargetChainId,
            reportContext.Sender,
            "0x3c37E0A09eAFEaA7eFB57107802De1B28A6f5F07"
            // reportContext.Receiver
        );
        return encoded;
    }

    public static byte[] GenerateTokenAmountBytes(TokenAmountDto tokenAmount)
    {
        var abiEncode = new ABIEncode();
        var encoded = abiEncode.GetABIEncoded(
            tokenAmount.SwapId,
            (int)tokenAmount.TargetChainId,
            tokenAmount.TargetContractAddress,
            tokenAmount.TokenAddress,
            tokenAmount.OriginToken,
            10000
            // tokenAmount.Amount
        );
        return encoded;
    }

    public static byte[] GenerateMessageBytes(string message) => Encoding.UTF8.GetBytes(message);

    public static ChainConfig GetChainConfig(long chainId, OracleInfoOptions options)
    {
        ChainConfig chainConfig = null;
        switch (chainId)
        {
            case 1:
                options.ChainConfig.TryGetValue("EVM", out chainConfig);
                break;
            case 56:
                options.ChainConfig.TryGetValue("BSC", out chainConfig);
                break;
            case 11155111:
                options.ChainConfig.TryGetValue("SEPOLIA", out chainConfig);
                break;
        }

        return chainConfig;
    }
}