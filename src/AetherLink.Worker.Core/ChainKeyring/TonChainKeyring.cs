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
        var unsignedCell = TonHelper.PopulateMetadata(new CellBuilder(),
            reportContext,
            report.Message,
            report.TokenTransferMetadata).Build();
        var secretKeyHex = Hex.Decode(_privateOptions.TransmitterSecretKey);
        return KeyPair.Sign(unsignedCell, secretKeyHex);
    }

    public override bool OffChainVerify(ReportContextDto reportContext, int index, CrossChainReportDto report,
        byte[] sign)
    {
        var bodyCell = TonHelper.PopulateMetadata(new CellBuilder(),
            reportContext,
            report.Message,
            report.TokenTransferMetadata).Build();

        var nodeInfo = _publicOption.OracleNodeInfoList.Find(f => f.Index == index);
        return nodeInfo != null && TonHelper.VerifySignature(nodeInfo.PublicKey, bodyCell, sign);
    }
}