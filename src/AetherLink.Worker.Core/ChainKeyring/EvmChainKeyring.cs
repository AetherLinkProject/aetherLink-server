using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.ChainKeyring;

public abstract class EvmBaseChainKeyring : ChainKeyring
{
    public abstract override long ChainId { get; }
    private readonly string[] _addressList;
    private readonly EvmOptions _evmOptions;

    protected EvmBaseChainKeyring(IOptionsSnapshot<EvmContractsOptions> evmOptions)
    {
        _addressList = evmOptions.Value.OracleNodeAddressList;
        _evmOptions = EvmHelper.GetEvmContractConfig(ChainId, evmOptions.Value);
    }

    public override byte[] OffChainSign(ReportContextDto reportContext, CrossChainReportDto report)
        => EvmHelper.OffChainSign(reportContext, report, _evmOptions);

    public override bool OffChainVerify(ReportContextDto reportContext, int index, CrossChainReportDto report,
        byte[] sign) => EvmHelper.OffChainVerify(reportContext, index, report, sign, _addressList, _evmOptions);
}

public class EvmChainKeyring : EvmBaseChainKeyring, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.EVM;

    public EvmChainKeyring(IOptionsSnapshot<EvmContractsOptions> evmOptions) : base(evmOptions)
    {
    }
}

public class SEPOLIAChainKeyring : EvmBaseChainKeyring, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.SEPOLIA;

    public SEPOLIAChainKeyring(IOptionsSnapshot<EvmContractsOptions> evmOptions) : base(evmOptions)
    {
    }
}

public class BaseSepoliaChainKeyring : EvmBaseChainKeyring, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.BASESEPOLIA;

    public BaseSepoliaChainKeyring(IOptionsSnapshot<EvmContractsOptions> evmOptions) : base(evmOptions)
    {
    }
}

public class BaseChainKeyring : EvmBaseChainKeyring, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.BASE;

    public BaseChainKeyring(IOptionsSnapshot<EvmContractsOptions> evmOptions) : base(evmOptions)
    {
    }
}

public class BscChainKeyring : EvmBaseChainKeyring, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.BSC;

    public BscChainKeyring(IOptionsSnapshot<EvmContractsOptions> evmOptions) : base(evmOptions)
    {
    }
}

public class BscTestChainKeyring : EvmBaseChainKeyring, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.BSCTEST;

    public BscTestChainKeyring(IOptionsSnapshot<EvmContractsOptions> evmOptions) : base(evmOptions)
    {
    }
}