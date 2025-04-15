using AElf;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using Google.Protobuf;
using Nethereum.ABI;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;

namespace AetherLink.Worker.Core.Common;

public class EvmHelper
{
    public static byte[] OffChainSign(ReportContextDto context, CrossChainReportDto report, EvmOptions chainConfig)
    {
        var reportHash = GenerateReportHash(context, report, chainConfig);
        var signer = new MessageSigner();
        var signature = signer.Sign(reportHash, chainConfig.SignerSecret);
        return ByteStringHelper.FromHexString(signature).ToByteArray();
    }

    public static bool OffChainVerify(ReportContextDto context, int index, CrossChainReportDto report, byte[] sign,
        string[] oracleNodeAddressList, EvmOptions chainConfig)
    {
        if (sign.Length <= 0 || index < 0 || index > oracleNodeAddressList.Length) return false;
        var reportHash = GenerateReportHash(context, report, chainConfig);
        var signer = new MessageSigner();
        var address = signer.EcRecover(reportHash, sign.ToHex());
        return oracleNodeAddressList[index] == address;
    }

    private static byte[] GenerateReportHash(ReportContextDto context, CrossChainReportDto report,
        EvmOptions chainConfig)
    {
        var reportContextDecoded = Sha3Keccack.Current.CalculateHash(GenerateReportContextBytes(context));
        var message = Sha3Keccack.Current.CalculateHash(GenerateMessageBytes(report.Message));
        var tokenTransferMetadataDecode =
            Sha3Keccack.Current.CalculateHash(GenerateTokenTransferMetadataBytes(report.TokenTransferMetadataDto));

        var abiEncode = new ABIEncode();
        var result = abiEncode.GetABIEncoded(
            new ABIValue("bytes32", ByteStringHelper.FromHexString(chainConfig.TransmitTypeHash).ToByteArray()),
            new ABIValue("bytes32", reportContextDecoded),
            new ABIValue("bytes32", message),
            new ABIValue("bytes32", tokenTransferMetadataDecode));
        var structHash = Sha3Keccack.Current.CalculateHash(result);

        var reportHashAbiEncode = new ABIEncode();
        var domainSeparatorBytes = ByteStringHelper.FromHexString(chainConfig.DomainSeparator).ToByteArray();
        var encoded = reportHashAbiEncode.GetABIEncodedPacked(
            new ABIValue("string", EvmTransactionConstants.EIP721Prefix),
            new ABIValue("bytes32", domainSeparatorBytes),
            new ABIValue("bytes32", structHash));
        return Sha3Keccack.Current.CalculateHash(encoded);
    }

    public static byte[] GenerateReportContextBytes(ReportContextDto reportContext)
    {
        var abiEncode = new ABIEncode();
        return abiEncode.GetABIEncoded(
            new ABIValue("bytes32", ByteStringHelper.FromHexString(reportContext.MessageId).ToByteArray()),
            new ABIValue("uint256", reportContext.SourceChainId),
            new ABIValue("uint256", reportContext.TargetChainId),
            new ABIValue("string", reportContext.Sender),
            new ABIValue("address", reportContext.Receiver));
    }

    public static byte[] GenerateTokenTransferMetadataBytes(TokenTransferMetadataDto tokenTransferMetadata)
    {
        if (tokenTransferMetadata == null) return new byte[] { };

        var abiEncode = new ABIEncode();
        return abiEncode.GetABIEncoded(
            new ABIValue("uint256", tokenTransferMetadata.TargetChainId),
            new ABIValue("string", tokenTransferMetadata.TokenAddress),
            new ABIValue("string", tokenTransferMetadata.Symbol),
            new ABIValue("uint256", tokenTransferMetadata.Amount),
            new ABIValue("bytes", ByteString.FromBase64(tokenTransferMetadata.ExtraDataString).ToByteArray())
        );
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
            case ChainIdConstants.BASESEPOLIA:
                options.ContractConfig.TryGetValue("BASESEPOLIA", out chainConfig);
                break;
        }

        return chainConfig;
    }
}