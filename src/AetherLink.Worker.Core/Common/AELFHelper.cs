using System.Threading.Tasks;
using AElf;
using AElf.Types;
using AetherLink.Contracts.Ramp;
using AetherLink.Multisignature;
using AetherLink.Worker.Core.Common.ContractHandler;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider;
using Google.Protobuf;
using Ramp;

namespace AetherLink.Worker.Core.Common;

public static class AELFHelper
{
    public static byte[] OffChainSign(ReportContextDto reportContext, CrossChainReportDto report,
        ChainConfig chainConfig)
    {
        var rpcTokenAmount = new TokenAmount();
        if (report.TokenAmount != null)
        {
            var temp = report.TokenAmount;
            rpcTokenAmount = new()
            {
                SwapId = temp.SwapId,
                TargetChainId = temp.TargetChainId,
                TargetContractAddress = temp.TargetContractAddress,
                TokenAddress = temp.TokenAddress,
                OriginToken = temp.OriginToken,
                Amount = temp.Amount
            };
        }

        var reportData = GenerateReport(reportContext, report.Message, rpcTokenAmount);
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

    public static async Task<ChainHandler.TransactionState> GetTransactionResultAsync(
        IContractProvider contractProvider, string chainId, string transactionId)
    {
        var txResult = await contractProvider.GetTxResultAsync(chainId, transactionId);
        return txResult.Status switch
        {
            TransactionState.Mined => ChainHandler.TransactionState.Success,
            TransactionState.Pending => ChainHandler.TransactionState.Pending,
            TransactionState.NotExisted => ChainHandler.TransactionState.NotExist,
            _ => ChainHandler.TransactionState.Fail
        };
    }

    public static ChainConfig GetChainConfig(long chainId, OracleInfoOptions options)
    {
        return options.ChainConfig.TryGetValue(ChainHelper.ConvertChainIdToBase58((int)chainId), out var chainConfig)
            ? chainConfig
            : null;
    }
}