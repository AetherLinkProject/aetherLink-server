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
            new ABIValue("bytes32", ByteString.FromBase64(reportContext.MessageId).ToByteArray()),
            new ABIValue("uint256", reportContext.SourceChainId),
            new ABIValue("uint256", reportContext.TargetChainId),
            new ABIValue("string", reportContext.Sender),
            new ABIValue("address", GenerateEvmAddressFormat(reportContext.Receiver)));
        return encoded;
    }

    private static string GenerateEvmAddressFormat(string receiver) => $"0x{ByteString.FromBase64(receiver).ToHex()}";

    public static byte[] GenerateTokenAmountBytes(TokenAmountDto tokenAmount)
    {
        var abiEncode = new ABIEncode();
        var encoded = abiEncode.GetABIEncoded(
            tokenAmount.SwapId,
            (int)tokenAmount.TargetChainId,
            tokenAmount.TargetContractAddress,
            tokenAmount.TokenAddress,
            tokenAmount.OriginToken,
            (int)tokenAmount.Amount
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