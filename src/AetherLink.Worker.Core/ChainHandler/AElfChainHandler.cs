using System.Collections.Generic;
using System.Threading.Tasks;
using AElf;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Provider;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.ChainHandler;

#region AElfBase

public abstract class AElfBaseChainWriter : ChainWriter
{
    public abstract override long ChainId { get; }
    private readonly IContractProvider _contractProvider;
    private readonly IOracleContractProvider _oracleContractProvider;

    protected AElfBaseChainWriter(IOracleContractProvider oracleContractProvider, IContractProvider contractProvider)
    {
        _oracleContractProvider = oracleContractProvider;
        _contractProvider = contractProvider;
    }

    public override async Task<string> SendCommitTransactionAsync(ReportContextDto reportContext,
        Dictionary<int, byte[]> signatures, CrossChainDataDto crossChainData)
        => await _contractProvider.SendCommitAsync(ChainHelper.ConvertChainIdToBase58((int)ChainId),
            await _oracleContractProvider.GenerateCommitDataAsync(reportContext, signatures, crossChainData));
}

public abstract class AElfBaseChainReader : ChainReader
{
    public abstract override long ChainId { get; }

    private readonly IContractProvider _contractProvider;

    protected AElfBaseChainReader(IContractProvider contractProvider)
    {
        _contractProvider = contractProvider;
    }

    public override Task<byte[]> CallTransactionAsync(byte[] transaction)
    {
        throw new System.NotImplementedException();
    }

    public override async Task<TransactionResultDto> GetTransactionResultAsync(string transactionId) => new()
    {
        State = await AELFHelper.GetTransactionResultAsync(_contractProvider,
            ChainHelper.ConvertChainIdToBase58((int)ChainId), transactionId)
    };

    public override string ConvertBytesToAddressStr(byte[] addressBytes)
    {
        return AElf.Types.Address.FromBytes(addressBytes).ToBase58();
    }
}

#endregion

#region Writer

public class AElfChainWriter : AElfBaseChainWriter, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.AELF;

    public AElfChainWriter(IOracleContractProvider oracleContractProvider, IContractProvider contractProvider)
        : base(oracleContractProvider, contractProvider)
    {
    }
}

public class TDVVChainWriter : AElfBaseChainWriter, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.TDVV;

    public TDVVChainWriter(IOracleContractProvider oracleContractProvider, IContractProvider contractProvider)
        : base(oracleContractProvider, contractProvider)
    {
    }
}

public class TDVWChainWriter : AElfBaseChainWriter, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.TDVW;

    public TDVWChainWriter(IOracleContractProvider oracleContractProvider, IContractProvider contractProvider)
        : base(oracleContractProvider, contractProvider)
    {
    }
}

#endregion

#region Reader

public class AElfChainReader : AElfBaseChainReader, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.AELF;

    public AElfChainReader(IContractProvider contractProvider) : base(contractProvider)
    {
    }
}

public class TDVVChainReader : AElfBaseChainReader, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.TDVV;

    public TDVVChainReader(IContractProvider contractProvider) : base(contractProvider)
    {
    }
}

public class TDVWChainReader : AElfBaseChainReader, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.TDVW;

    public TDVWChainReader(IContractProvider contractProvider) : base(contractProvider)
    {
    }
}

#endregion