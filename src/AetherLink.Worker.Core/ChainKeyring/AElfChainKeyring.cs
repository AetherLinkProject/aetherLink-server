using AElf;
using AetherLink.Contracts.Ramp;
using AetherLink.Multisignature;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using Google.Protobuf;
using Microsoft.Extensions.Options;
using Ramp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.ChainKeyring;

public class AElfChainKeyring : ChainKeyring, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.AELF;

    private readonly ChainConfig _chainConfig;
    private readonly IObjectMapper _objectMapper;

    public AElfChainKeyring(IOptionsSnapshot<OracleInfoOptions> oracleOptions, IObjectMapper objectMapper)
    {
        _objectMapper = objectMapper;
        _chainConfig = oracleOptions.Value.ChainConfig[ChainHelper.ConvertChainIdToBase58((int)ChainIdConstants.AELF)];
    }

    public override byte[] OffChainSign(ReportContextDto reportContext, CrossChainReportDto report)
    {
        var reportData = new Report
        {
            ReportContext = new()
            {
                MessageId = HashHelper.ComputeFrom(reportContext.MessageId),
                SourceChainId = reportContext.SourceChainId,
                TargetChainId = reportContext.TargetChainId,
                Sender = ByteString.FromBase64(reportContext.Sender),
                Receiver = ByteString.FromBase64(reportContext.Receiver)
            },
            Message = ByteString.FromBase64(report.Message),
            TokenAmount = _objectMapper.Map<TokenAmountDto, TokenAmount>(report.TokenAmount)
        };

        var msg = HashHelper.ComputeFrom(reportData.ToByteArray()).ToByteArray();
        var multiSignature = new MultiSignature(ByteArrayHelper.HexStringToByteArray(_chainConfig.SignerSecret), msg,
            _chainConfig.DistPublicKey, _chainConfig.PartialSignaturesThreshold);
        return multiSignature.GeneratePartialSignature().Signature;
    }

    public override bool OffChainVerify(ReportContextDto reportContext, int index, CrossChainReportDto report,
        byte[] sign)
    {
        return true;
    }
}