using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.ChainHandler;

#region EvmBase

public abstract class EvmBaseChainWriter : ChainWriter
{
    public abstract override long ChainId { get; }
    private readonly IEvmProvider _evmProvider;
    private readonly EvmOptions _evmOptions;

    protected EvmBaseChainWriter(IEvmProvider evmProvider, IOptionsSnapshot<EvmContractsOptions> optionsSnapshot)
    {
        _evmProvider = evmProvider;
        _evmOptions = EvmHelper.GetEvmContractConfig(ChainId, optionsSnapshot.Value);
    }

    public override async Task<string> SendCommitTransactionAsync(ReportContextDto reportContext,
        Dictionary<int, byte[]> signatures, CrossChainDataDto crossChainData)
    {
        var contextBytes = EvmHelper.GenerateReportContextBytes(reportContext);
        var messageBytes = EvmHelper.GenerateMessageBytes(crossChainData.Message);
        var tokenTransferMetadataBytes =
            EvmHelper.GenerateTokenTransferMetadataBytes(crossChainData.TokenTransferMetadata);

        return await _evmProvider.TransmitAsync(
            _evmOptions,
            contextBytes,
            messageBytes,
            tokenTransferMetadataBytes,
            signatures.Values.ToArray()
        );
    }
}

public abstract class EvmBaseChainReader : ChainReader
{
    public abstract override long ChainId { get; }

    private readonly IEvmProvider _evmProvider;
    private readonly EvmOptions _evmOptions;

    protected EvmBaseChainReader(IEvmProvider evmProvider, IOptionsSnapshot<EvmContractsOptions> optionsSnapshot)
    {
        _evmProvider = evmProvider;
        _evmOptions = EvmHelper.GetEvmContractConfig(ChainId, optionsSnapshot.Value);
    }

    public override async Task<TransactionResultDto> GetTransactionResultAsync(string transactionId)
        => new() { State = await _evmProvider.GetTransactionResultAsync(_evmOptions, transactionId) };

    public override string ConvertBytesToAddressStr(byte[] addressBytes)
    {
        return AElf.Types.Address.FromBytes(addressBytes).ToBase58();
    }

    public override Task<byte[]> CallTransactionAsync(byte[] transaction)
    {
        throw new System.NotImplementedException();
    }
}

#endregion

#region Writer

// Writer
public class EvmChainWriter : EvmBaseChainWriter, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.EVM;

    public EvmChainWriter(IEvmProvider evmProvider, IOptionsSnapshot<EvmContractsOptions> evmOptions)
        : base(evmProvider, evmOptions)
    {
    }
}

public class BscChainWriter : EvmBaseChainWriter, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.BSC;

    public BscChainWriter(IEvmProvider evmProvider, IOptionsSnapshot<EvmContractsOptions> evmOptions)
        : base(evmProvider, evmOptions)
    {
    }
}

public class SEPOLIAChainWriter : EvmBaseChainWriter, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.SEPOLIA;

    public SEPOLIAChainWriter(IEvmProvider evmProvider, IOptionsSnapshot<EvmContractsOptions> evmOptions)
        : base(evmProvider, evmOptions)
    {
    }
}

public class BscTestChainWriter : EvmBaseChainWriter, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.BSCTEST;

    public BscTestChainWriter(IEvmProvider evmProvider, IOptionsSnapshot<EvmContractsOptions> evmOptions)
        : base(evmProvider, evmOptions)
    {
    }
}

public class BaseSepoliaWriter : EvmBaseChainWriter, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.BASESEPOLIA;

    public BaseSepoliaWriter(IEvmProvider evmProvider, IOptionsSnapshot<EvmContractsOptions> evmOptions)
        : base(evmProvider, evmOptions)
    {
    }
}

#endregion

#region Reader

public class EvmChainReader : EvmBaseChainReader, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.EVM;

    public EvmChainReader(IEvmProvider evmProvider, IOptionsSnapshot<EvmContractsOptions> evmOptions)
        : base(evmProvider, evmOptions)
    {
    }
}

public class BscChainReader : EvmBaseChainReader, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.BSC;

    public BscChainReader(IEvmProvider evmProvider, IOptionsSnapshot<EvmContractsOptions> evmOptions)
        : base(evmProvider, evmOptions)
    {
    }
}

public class BscTestChainReader : EvmBaseChainReader, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.BSCTEST;

    public BscTestChainReader(IEvmProvider evmProvider, IOptionsSnapshot<EvmContractsOptions> evmOptions)
        : base(evmProvider, evmOptions)
    {
    }
}

public class SEPOLIAChainReader : EvmBaseChainReader, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.SEPOLIA;

    public SEPOLIAChainReader(IEvmProvider evmProvider, IOptionsSnapshot<EvmContractsOptions> evmOptions)
        : base(evmProvider, evmOptions)
    {
    }
}

public class BaseSepoliaChainReader : EvmBaseChainReader, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.BASESEPOLIA;

    public BaseSepoliaChainReader(IEvmProvider evmProvider, IOptionsSnapshot<EvmContractsOptions> evmOptions)
        : base(evmProvider, evmOptions)
    {
    }
}

#endregion