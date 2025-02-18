using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using Nethereum.ABI;
using Nethereum.Signer;
using Nethereum.Util;

namespace AetherLink.Worker.Core.Common;

public class EvmHelper
{
    public static byte[] OffChainSign(ReportContextDto reportContext, CrossChainReportDto report,
        ChainConfig chainConfig)
    {
        var reportHash = ComputeReportHash(reportContext, report.TokenAmount, report.Message);
        var signer = new EthereumMessageSigner();
        var signature = signer.Sign(reportHash, chainConfig.SignerSecret);
        return StringToByteArray(signature);
    }

    public static EvmSignatureDto AggregateSignatures(List<byte[]> aggregatedSignatures)
    {
        var rs = new List<string>();
        var ss = new List<string>();
        var vs = new List<byte>();

        foreach (var signature in aggregatedSignatures)
        {
            if (signature.Length != 65)
            {
                throw new ArgumentException("Invalid signature length. Expected 65 bytes.");
            }

            var r = BitConverter.ToString(signature[..32]).Replace("-", "");
            var s = BitConverter.ToString(signature[32..64]).Replace("-", "");
            var v = signature[64];
            rs.Add(r);
            ss.Add(s);
            vs.Add(v);
        }

        return new()
        {
            R = rs.ToArray(),
            S = ss.ToArray(),
            V = ConvertVsToBytes32(vs)
        };
    }

    private static string ConvertVsToBytes32(List<byte> vs)
    {
        var buffer = new byte[32];
        for (var i = 0; i < vs.Count; i++)
        {
            buffer[31 - i] = vs[i];
        }

        return "0x" + BitConverter.ToString(buffer).Replace("-", "").ToLower();
    }

    private static byte[] ComputeReportHash(ReportContextDto reportContext, TokenAmountDto tokenAmount, string message)
    {
        var abiEncoder = new ABIEncode();

        var encodedReportContext = abiEncoder.GetABIEncoded(
            new[] { "bytes32", "uint256", "uint256", "string", "address" },
            new object[]
            {
                reportContext.MessageId, // bytes32
                new BigInteger(reportContext.SourceChainId), // uint256
                new BigInteger(reportContext.TargetChainId), // uint256
                reportContext.Sender, // string
                reportContext.Receiver // address
            });

        var encodedTokenAmount = abiEncoder.GetABIEncoded(
            new[] { "string", "uint256", "string", "string", "string", "uint256" },
            new object[]
            {
                tokenAmount.SwapId, // string
                new BigInteger(tokenAmount.TargetChainId), // uint256
                tokenAmount.TargetContractAddress, // string
                tokenAmount.TokenAddress, // string
                tokenAmount.OriginToken, // string
                tokenAmount.Amount // uint256
            });

        var encodedMessage = abiEncoder.GetABIEncoded(new[] { "string" }, new object[] { message });
        var combinedBytes = CombineBytes(encodedReportContext, encodedMessage, encodedTokenAmount);
        return new Sha3Keccack().CalculateHash(combinedBytes);
    }

    private static byte[] StringToByteArray(string hex)
    {
        if (hex.StartsWith("0x"))
        {
            hex = hex[2..];
        }

        var result = new byte[hex.Length / 2];
        for (var i = 0; i < hex.Length; i += 2)
        {
            result[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        }

        return result;
    }

    private static byte[] CombineBytes(params byte[][] arrays)
    {
        var combinedLength = arrays.Sum(arr => arr.Length);
        var combined = new byte[combinedLength];
        var offset = 0;

        foreach (var array in arrays)
        {
            Buffer.BlockCopy(array, 0, combined, offset, array.Length);
            offset += array.Length;
        }

        return combined;
    }

    public static ChainConfig GetChainConfig(long chainId, OracleInfoOptions options)
    {
        // todo: convert long to string
        return options.ChainConfig.TryGetValue("", out var chainConfig)
            ? chainConfig
            : null;
    }
}