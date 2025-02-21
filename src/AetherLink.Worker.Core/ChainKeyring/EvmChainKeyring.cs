using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.ChainKeyring;

public class EvmChainKeyring : ChainKeyring, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.EVM;
    private readonly ChainConfig _chainConfig;

    public EvmChainKeyring(IOptionsSnapshot<OracleInfoOptions> oracleOptions)
    {
        _chainConfig = EvmHelper.GetChainConfig(ChainId, oracleOptions.Value);
    }

    public override byte[] OffChainSign(ReportContextDto reportContext, CrossChainReportDto report)
        => EvmHelper.OffChainSign(reportContext, report, _chainConfig);

    public override bool OffChainVerify(ReportContextDto reportContext, int index, CrossChainReportDto report,
        byte[] sign) => true;
}

public class SEPOLIAChainKeyring : ChainKeyring, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.SEPOLIA;
    private readonly ChainConfig _chainConfig;

    public SEPOLIAChainKeyring(IOptionsSnapshot<OracleInfoOptions> oracleOptions)
    {
        _chainConfig = EvmHelper.GetChainConfig(ChainId, oracleOptions.Value);
    }

    public override byte[] OffChainSign(ReportContextDto reportContext, CrossChainReportDto report)
        => EvmHelper.OffChainSign(reportContext, report, _chainConfig);

    public override bool OffChainVerify(ReportContextDto reportContext, int index, CrossChainReportDto report,
        byte[] sign) => true;
}

public class BscChainKeyring : ChainKeyring, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.BSC;
    private readonly ChainConfig _chainConfig;

    public BscChainKeyring(IOptionsSnapshot<OracleInfoOptions> oracleOptions)
    {
        _chainConfig = EvmHelper.GetChainConfig(ChainId, oracleOptions.Value);
    }

    public override byte[] OffChainSign(ReportContextDto reportContext, CrossChainReportDto report)
        => EvmHelper.OffChainSign(reportContext, report, _chainConfig);

    public override bool OffChainVerify(ReportContextDto reportContext, int index, CrossChainReportDto report,
        byte[] sign) => true;
}