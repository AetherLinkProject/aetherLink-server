using AetherLink.Worker.Core.Dtos;

namespace AetherLink.Worker.Core.ChainKeyring;

public interface IChainKeyring
{
    long ChainId { get; }
    byte[] OffChainSign(ReportContextDto reportContext, CrossChainReportDto report);
    bool OffChainVerify(ReportContextDto reportContext, int index, CrossChainReportDto report, byte[] sign);
}

public abstract class ChainKeyring : IChainKeyring
{
    public abstract long ChainId { get; }
    public abstract byte[] OffChainSign(ReportContextDto reportContext, CrossChainReportDto report);

    public abstract bool OffChainVerify(ReportContextDto reportContext, int index, CrossChainReportDto report,
        byte[] sign);
}