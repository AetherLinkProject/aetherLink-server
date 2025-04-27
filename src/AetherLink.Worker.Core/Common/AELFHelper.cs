using System.Threading;
using System.Threading.Tasks;
using AElf;
using AElf.Types;
using AetherLink.Contracts.Ramp;
using AetherLink.Multisignature;
using AetherLink.Worker.Core.Common.ContractHandler;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider;
using Google.Protobuf;
using TokenTransferMetadata = Ramp.TokenTransferMetadata;

namespace AetherLink.Worker.Core.Common;

public static class AELFHelper
{
    public static byte[] OffChainSign(ReportContextDto reportContext, CrossChainReportDto report,
        ChainConfig chainConfig)
    {
        var tokenTransferMetadata = new TokenTransferMetadata();
        if (report.TokenTransferMetadataDto != null)
        {
            var temp = report.TokenTransferMetadataDto;
            tokenTransferMetadata = new()
            {
                ExtraData = ByteString.FromBase64(temp.ExtraDataString),
                TargetChainId = temp.TargetChainId,
                TokenAddress = temp.TokenAddress,
                Symbol = temp.Symbol,
                Amount = temp.Amount
            };
        }

        var reportData = GenerateReport(reportContext, report.Message, tokenTransferMetadata);
        var msg = HashHelper.ComputeFrom(reportData.ToByteArray()).ToByteArray();
        var multiSignature = new MultiSignature(ByteArrayHelper.HexStringToByteArray(chainConfig.SignerSecret), msg,
            chainConfig.DistPublicKey, chainConfig.PartialSignaturesThreshold);
        return multiSignature.GeneratePartialSignature().Signature;
    }

    public static bool OffChainVerify(ReportContextDto reportContext, CrossChainReportDto report,
        int index, byte[] sign, ChainConfig chainConfig)
    {
        var msg = GenerateMessage(reportContext, report);
        var multiSignature = new MultiSignature(ByteArrayHelper.HexStringToByteArray(chainConfig.SignerSecret), msg,
            chainConfig.DistPublicKey, chainConfig.PartialSignaturesThreshold);
        return multiSignature.ProcessPartialSignature(new()
        {
            Signature = sign,
            Index = index
        });
    }

    private static byte[] GenerateMessage(ReportContextDto reportContext, CrossChainReportDto report)
    {
        var tokenTransferMetadata = new TokenTransferMetadata();
        if (report.TokenTransferMetadataDto != null)
        {
            var temp = report.TokenTransferMetadataDto;
            tokenTransferMetadata = new()
            {
                TargetChainId = temp.TargetChainId,
                TokenAddress = temp.TokenAddress,
                Symbol = temp.Symbol,
                Amount = temp.Amount,
                ExtraData = ByteString.FromBase64(temp.ExtraDataString)
            };
        }

        var reportData = GenerateReport(reportContext, report.Message, tokenTransferMetadata);
        return HashHelper.ComputeFrom(reportData.ToByteArray()).ToByteArray();
    }

    public static Report GenerateReport(ReportContextDto reportContext, string message,
        TokenTransferMetadata tokenTransferMetadata)
    {
        var report = new Report()
        {
            ReportContext = new()
            {
                SourceChainId = reportContext.SourceChainId,
                TargetChainId = reportContext.TargetChainId,
                Sender = ByteString.FromBase64(reportContext.Sender),
                Receiver = Address.FromBase58(reportContext.Receiver).ToByteString()
            },
            Message = ByteString.FromBase64(message),
            TokenTransferMetadata = tokenTransferMetadata
        };
        report.ReportContext.MessageId = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(report),
            HashHelper.ComputeFrom(reportContext.MessageId));

        return report;
    }

    public static async Task<ChainHandler.TransactionState> GetTransactionResultAsync(
        IContractProvider contractProvider, string chainId, string transactionId)
    {
        for (var i = 0; i < RetryConstants.MaximumRetryTimes; i++)
        {
            var txResult = await contractProvider.GetTxResultAsync(chainId, transactionId);
            switch (txResult.Status)
            {
                case TransactionState.Pending:
                case TransactionState.NotExisted:
                    await Task.Delay((i + 1) * 1000 * 2);
                    break;
                case TransactionState.Mined:
                    return ChainHandler.TransactionState.Success;
                default:
                    return ChainHandler.TransactionState.Fail;
            }
        }

        return ChainHandler.TransactionState.Fail;
    }

    public static ChainConfig GetChainConfig(long chainId, OracleInfoOptions options)
    {
        return options.ChainConfig.TryGetValue(ChainHelper.ConvertChainIdToBase58((int)chainId), out var chainConfig)
            ? chainConfig
            : null;
    }
}