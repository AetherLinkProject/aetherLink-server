using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.ChainKeyring;

public abstract class AElfBaseChainKeyring : ChainKeyring
{
    public abstract override long ChainId { get; }
    private readonly ChainConfig _chainConfig;

    protected AElfBaseChainKeyring(IOptionsSnapshot<OracleInfoOptions> oracleOptions)
    {
        _chainConfig = AELFHelper.GetChainConfig(ChainId, oracleOptions.Value);
    }

    public override byte[] OffChainSign(ReportContextDto reportContext, CrossChainReportDto report)
        => AELFHelper.OffChainSign(reportContext, report, _chainConfig);

    public override bool OffChainVerify(ReportContextDto reportContext, int index, CrossChainReportDto report,
        byte[] sign) => AELFHelper.OffChainVerify(reportContext, report, index, sign, _chainConfig);
}

public class AElfChainKeyring : AElfBaseChainKeyring, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.AELF;

    public AElfChainKeyring(IOptionsSnapshot<OracleInfoOptions> oracleOptions) : base(oracleOptions)
    {
    }
}

public class TDVWChainKeyring : AElfBaseChainKeyring, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.TDVW;

    public TDVWChainKeyring(IOptionsSnapshot<OracleInfoOptions> oracleOptions) : base(oracleOptions)
    {
    }
}

public class TDVVChainKeyring : AElfBaseChainKeyring, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.TDVV;

    public TDVVChainKeyring(IOptionsSnapshot<OracleInfoOptions> oracleOptions) : base(oracleOptions)
    {
    }
}