using AElf;
using AElf.Types;
using AetherLink.Contracts.Ramp;
using AetherLink.Multisignature;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using Google.Protobuf;
using Ramp;

namespace AetherLink.Worker.Core.Common;

public static class AELFHelper
{
    public static byte[] OffChainSign(ReportContextDto reportContext, CrossChainReportDto report,
        TokenAmount tokenAmount, ChainConfig chainConfig)
    {
        var reportData = GenerateReport(reportContext, report.Message, tokenAmount);
        var msg = HashHelper.ComputeFrom(reportData.ToByteArray()).ToByteArray();
        var multiSignature = new MultiSignature(ByteArrayHelper.HexStringToByteArray(chainConfig.SignerSecret), msg,
            chainConfig.DistPublicKey, chainConfig.PartialSignaturesThreshold);
        return multiSignature.GeneratePartialSignature().Signature;
    }

    public static Report GenerateReport(ReportContextDto reportContext, string message, TokenAmount tokenAmount)
    {
        return new()
        {
            ReportContext = new()
            {
                MessageId = HashHelper.ComputeFrom(reportContext.MessageId),
                SourceChainId = reportContext.SourceChainId,
                TargetChainId = reportContext.TargetChainId,
                Sender = ByteString.FromBase64(reportContext.Sender),
                Receiver = Address.FromBase58(reportContext.Receiver).ToByteString()
            },
            Message = ByteString.FromBase64(message),
            TokenAmount = tokenAmount
        };
    }
    
}