using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using Volo.Abp.DependencyInjection;
using System;
using System.Numerics;
using System.Text;
using AElf;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Options;
using Google.Protobuf;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Utilities.Encoders;
using TonSdk.Core;
using TonSdk.Core.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace AetherLink.Worker.Core.ChainKeyring;

public class TonChainKeyring : ChainKeyring, ISingletonDependency
{
    private readonly TonPublicOptions _publicOption;
    private readonly TonPrivateOptions _privateOptions;
    public override long ChainId => ChainIdConstants.TON;

    public TonChainKeyring(IOptionsSnapshot<TonPrivateOptions> privateOptions,
        IOptionsSnapshot<TonPublicOptions> publicConfig)
    {
        _privateOptions = privateOptions.Value;
        _publicOption = publicConfig.Value;
    }

    public override byte[] OffChainSign(ReportContextDto reportContext, CrossChainReportDto report)
    {
        var unsignedCell = TonHelper.BuildUnsignedCell(
            new BigInteger(new ReadOnlySpan<byte>(Base64.Decode(reportContext.MessageId)), false, true),
            reportContext.SourceChainId,
            reportContext.TargetChainId,
            Base58CheckEncoding.Decode(reportContext.Sender),
            TonHelper.ConvertAddress(reportContext.Receiver),
            Base64.Decode(report.Message),
            report.TokenAmount);

        return KeyPair.Sign(unsignedCell, Hex.Decode(_privateOptions.TransmitterSecretKey));
    }

    public override bool OffChainVerify(ReportContextDto reportContext, int index, CrossChainReportDto report,
        byte[] sign)
    {
        var bodyCell = TonHelper.BuildUnsignedCell(
            new BigInteger(new ReadOnlySpan<byte>(Base64.Decode(reportContext.MessageId)), false, true),
            reportContext.SourceChainId,
            reportContext.TargetChainId,
            Base58CheckEncoding.Decode(reportContext.Sender),
            TonHelper.ConvertAddress(reportContext.Receiver),
            Base64.Decode(report.Message),
            report.TokenAmount);

        var nodeInfo = _publicOption.OracleNodeInfoList.Find(f => f.Index == index);
        if (nodeInfo == null) return false;

        var publicKeyParameters = new Ed25519PublicKeyParameters(Hex.Decode(nodeInfo.PublicKey));
        var signer = new Ed25519Signer();
        signer.Init(false, publicKeyParameters);
        var hash = bodyCell.Hash.ToBytes();
        signer.BlockUpdate(hash, 0, hash.Length);

        return signer.VerifySignature(sign);
    }
}