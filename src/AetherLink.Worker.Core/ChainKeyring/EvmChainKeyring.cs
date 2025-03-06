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
    private readonly EvmOptions _evmOptions;

    public EvmChainKeyring(IOptionsSnapshot<EvmContractsOptions> evmOptions)
    {
        _evmOptions = EvmHelper.GetEvmContractConfig(ChainId, evmOptions.Value);
    }

    public override byte[] OffChainSign(ReportContextDto reportContext, CrossChainReportDto report)
        => EvmHelper.OffChainSign(reportContext, report, _evmOptions);

    public override bool OffChainVerify(ReportContextDto reportContext, int index, CrossChainReportDto report,
        byte[] sign) => true;
}

public class SEPOLIAChainKeyring : ChainKeyring, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.SEPOLIA;
    private readonly EvmOptions _evmOptions;

    public SEPOLIAChainKeyring(IOptionsSnapshot<EvmContractsOptions> evmOptions)
    {
        _evmOptions = EvmHelper.GetEvmContractConfig(ChainId, evmOptions.Value);
    }

    public override byte[] OffChainSign(ReportContextDto reportContext, CrossChainReportDto report)
        => EvmHelper.OffChainSign(reportContext, report, _evmOptions);

    public override bool OffChainVerify(ReportContextDto reportContext, int index, CrossChainReportDto report,
        byte[] sign) => true;
}

public class BscChainKeyring : ChainKeyring, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.BSC;
    private readonly EvmOptions _evmOptions;

    public BscChainKeyring(IOptionsSnapshot<EvmContractsOptions> evmOptions)
    {
        _evmOptions = EvmHelper.GetEvmContractConfig(ChainId, evmOptions.Value);
    }

    public override byte[] OffChainSign(ReportContextDto reportContext, CrossChainReportDto report)
        => EvmHelper.OffChainSign(reportContext, report, _evmOptions);

    public override bool OffChainVerify(ReportContextDto reportContext, int index, CrossChainReportDto report,
        byte[] sign) => true;
}

public class BscTestChainKeyring : ChainKeyring, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.BSCTEST;
    private readonly EvmOptions _evmOptions;

    public BscTestChainKeyring(IOptionsSnapshot<EvmContractsOptions> evmOptions)
    {
        _evmOptions = EvmHelper.GetEvmContractConfig(ChainId, evmOptions.Value);
    }

    public override byte[] OffChainSign(ReportContextDto reportContext, CrossChainReportDto report)
        => EvmHelper.OffChainSign(reportContext, report, _evmOptions);

    public override bool OffChainVerify(ReportContextDto reportContext, int index, CrossChainReportDto report,
        byte[] sign) => true;
}