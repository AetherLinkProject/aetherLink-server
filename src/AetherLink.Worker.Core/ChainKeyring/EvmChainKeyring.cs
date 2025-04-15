using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.ChainKeyring;

public abstract class EvmBaseChainKeyring : ChainKeyring
{
    public abstract override long ChainId { get; }
    private readonly EvmOptions _evmOptions;
    private readonly string[] _distPublicKey;
    private ILogger _logger;

    protected EvmBaseChainKeyring(IOptionsSnapshot<EvmContractsOptions> evmOptions, ILogger logger)
    {
        _logger = logger;
        _distPublicKey = evmOptions.Value.DistPublicKey;
        _evmOptions = EvmHelper.GetEvmContractConfig(ChainId, evmOptions.Value);
    }

    public override byte[] OffChainSign(ReportContextDto reportContext, CrossChainReportDto report)
    {
        var contextBase64 = ByteString.CopyFrom(EvmHelper.GenerateReportContextBytes(reportContext)).ToBase64();
        var messageBase64 = ByteString.CopyFrom(EvmHelper.GenerateMessageBytes(report.Message)).ToBase64();
        var tokenMetaBase64 = ByteString
            .CopyFrom(EvmHelper.GenerateTokenTransferMetadataBytes(report.TokenTransferMetadataDto)).ToBase64();
        _logger.LogInformation($"[OffChainSign] {contextBase64} {messageBase64} {tokenMetaBase64}");

        return EvmHelper.OffChainSign(reportContext, report, _evmOptions);
    }

    public override bool OffChainVerify(ReportContextDto reportContext, int index, CrossChainReportDto report,
        byte[] sign) => EvmHelper.OffChainVerify(reportContext, index, report, sign, _distPublicKey, _evmOptions);
}

public class EvmChainKeyring : EvmBaseChainKeyring, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.EVM;

    public EvmChainKeyring(IOptionsSnapshot<EvmContractsOptions> evmOptions, ILogger logger) : base(evmOptions, logger)
    {
    }
}

public class SEPOLIAChainKeyring : EvmBaseChainKeyring, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.SEPOLIA;

    public SEPOLIAChainKeyring(IOptionsSnapshot<EvmContractsOptions> evmOptions, ILogger logger) : base(evmOptions,
        logger)
    {
    }
}

public class BaseSepoliaChainKeyring : EvmBaseChainKeyring, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.BASESEPOLIA;

    public BaseSepoliaChainKeyring(IOptionsSnapshot<EvmContractsOptions> evmOptions, ILogger logger) : base(evmOptions,
        logger)
    {
    }
}

public class BscChainKeyring : EvmBaseChainKeyring, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.BSC;

    public BscChainKeyring(IOptionsSnapshot<EvmContractsOptions> evmOptions, ILogger logger) : base(evmOptions, logger)
    {
    }
}

public class BscTestChainKeyring : EvmBaseChainKeyring, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.BSCTEST;

    public BscTestChainKeyring(IOptionsSnapshot<EvmContractsOptions> evmOptions, ILogger logger) : base(evmOptions,
        logger)
    {
    }
}