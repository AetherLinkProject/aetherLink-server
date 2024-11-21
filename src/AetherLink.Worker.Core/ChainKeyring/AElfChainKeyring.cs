using AElf;
using AElf.Types;
using AetherLink.Contracts.Ramp;
using AetherLink.Multisignature;
using AetherLink.Worker.Core.Common;
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
        => AELFHelper.OffChainSign(reportContext, report, _chainConfig);

    public override bool OffChainVerify(ReportContextDto reportContext, int index, CrossChainReportDto report,
        byte[] sign)
    {
        return true;
    }
}

public class TDVWChainKeyring : ChainKeyring, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.TDVW;

    private readonly ChainConfig _chainConfig;
    private readonly IObjectMapper _objectMapper;

    public TDVWChainKeyring(IOptionsSnapshot<OracleInfoOptions> oracleOptions, IObjectMapper objectMapper)
    {
        _objectMapper = objectMapper;
        _chainConfig = oracleOptions.Value.ChainConfig[ChainHelper.ConvertChainIdToBase58((int)ChainIdConstants.TDVW)];
    }

    public override byte[] OffChainSign(ReportContextDto reportContext, CrossChainReportDto report)
        => AELFHelper.OffChainSign(reportContext, report, _chainConfig);

    public override bool OffChainVerify(ReportContextDto reportContext, int index, CrossChainReportDto report,
        byte[] sign)
    {
        return true;
    }
}

public class TDVVChainKeyring : ChainKeyring, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.TDVV;

    private readonly ChainConfig _chainConfig;
    private readonly IObjectMapper _objectMapper;

    public TDVVChainKeyring(IOptionsSnapshot<OracleInfoOptions> oracleOptions, IObjectMapper objectMapper)
    {
        _objectMapper = objectMapper;
        _chainConfig = oracleOptions.Value.ChainConfig[ChainHelper.ConvertChainIdToBase58((int)ChainIdConstants.TDVV)];
    }

    public override byte[] OffChainSign(ReportContextDto reportContext, CrossChainReportDto report)
        => AELFHelper.OffChainSign(reportContext, report, _chainConfig);

    public override bool OffChainVerify(ReportContextDto reportContext, int index, CrossChainReportDto report,
        byte[] sign)
    {
        return true;
    }
}