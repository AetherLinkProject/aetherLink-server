using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using Volo.Abp.DependencyInjection;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Options;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Utilities.Encoders;
using TonSdk.Core.Crypto;
using TonSdk.Core.Boc;

namespace AetherLink.Worker.Core.ChainKeyring;

public class TonChainKeyring : ChainKeyring, ISingletonDependency
{
    private readonly TonPublicOptions _publicOption;
    private readonly TonPrivateOptions _privateOptions;
    public override long ChainId => ChainIdConstants.TON;

    public TonChainKeyring(IOptionsSnapshot<TonPrivateOptions> privateOptions,
        IOptionsSnapshot<TonPublicOptions> publicConfig)
    {
        _publicOption = publicConfig.Value;
        _privateOptions = privateOptions.Value;
    }

    public override byte[] OffChainSign(ReportContextDto reportContext, CrossChainReportDto report)
    {
        var meta = TonHelper.ConstructDataToSign(reportContext, report.Message, report.TokenTransferMetadataDto);
        var secretKeyHex = Hex.Decode(_privateOptions.TransmitterSecretKey);
        return KeyPair.Sign(meta, secretKeyHex);
    }

    public override bool OffChainVerify(ReportContextDto reportContext, int index, CrossChainReportDto report,
        byte[] sign)
    {
        var meta = TonHelper.ConstructDataToSign(reportContext, report.Message, report.TokenTransferMetadataDto);
        var nodeInfo = _publicOption.OracleNodeInfoList.Find(f => f.Index == index);
        return nodeInfo != null && TonHelper.VerifySignature(nodeInfo.PublicKey, meta, sign);
    }
}