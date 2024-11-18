using System.Collections.Generic;
using System.Threading.Tasks;
using AElf;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Provider;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.ChainHandler;

// Writer
public class AElfChainWriter : ChainWriter, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.AELF;
    private readonly IContractProvider _contractProvider;
    private readonly IOracleContractProvider _oracleContractProvider;

    public AElfChainWriter(IOracleContractProvider oracleContractProvider, IContractProvider contractProvider)
    {
        _oracleContractProvider = oracleContractProvider;
        _contractProvider = contractProvider;
    }

    public override async Task<string> SendCommitTransactionAsync(ReportContextDto reportContext,
        Dictionary<int, byte[]> signatures, CrossChainDataDto crossChainData)
        => await _contractProvider.SendCommitAsync(ChainHelper.ConvertChainIdToBase58((int)ChainId),
            await _oracleContractProvider.GenerateCommitDataAsync(reportContext, signatures, crossChainData));
}

public class TDVVChainWriter : ChainWriter, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.TDVV;
    private readonly IContractProvider _contractProvider;
    private readonly IOracleContractProvider _oracleContractProvider;

    public TDVVChainWriter(IOracleContractProvider oracleContractProvider, IContractProvider contractProvider)
    {
        _contractProvider = contractProvider;
        _oracleContractProvider = oracleContractProvider;
    }

    public override async Task<string> SendCommitTransactionAsync(ReportContextDto reportContext,
        Dictionary<int, byte[]> signatures, CrossChainDataDto crossChainData)
        => await _contractProvider.SendCommitAsync(ChainHelper.ConvertChainIdToBase58((int)ChainId),
            await _oracleContractProvider.GenerateCommitDataAsync(reportContext, signatures, crossChainData));
}

public class TDVWChainWriter : ChainWriter, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.TDVW;
    private readonly IContractProvider _contractProvider;
    private readonly IOracleContractProvider _oracleContractProvider;

    public TDVWChainWriter(IOracleContractProvider oracleContractProvider, IContractProvider contractProvider)
    {
        _contractProvider = contractProvider;
        _oracleContractProvider = oracleContractProvider;
    }

    public override async Task<string> SendCommitTransactionAsync(ReportContextDto reportContext,
        Dictionary<int, byte[]> signatures, CrossChainDataDto crossChainData)
        => await _contractProvider.SendCommitAsync(ChainHelper.ConvertChainIdToBase58((int)ChainId),
            await _oracleContractProvider.GenerateCommitDataAsync(reportContext, signatures, crossChainData));
}

// Reader
public class AElfChainReader : ChainReader, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.AELF;

    public override Task<byte[]> CallTransactionAsync(byte[] transaction)
    {
        throw new System.NotImplementedException();
    }

    public override async Task<TransactionResultDto> GetTransactionResultAsync(string transactionId)
    {
        return new() { State = TransactionState.Success };
    }
}

public class TDVVChainReader : ChainReader, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.TDVV;

    public override Task<byte[]> CallTransactionAsync(byte[] transaction)
    {
        throw new System.NotImplementedException();
    }

    public override async Task<TransactionResultDto> GetTransactionResultAsync(string transactionId)
    {
        return new() { State = TransactionState.Success };
    }
}

public class TDVWChainReader : ChainReader, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.TDVW;

    public override Task<byte[]> CallTransactionAsync(byte[] transaction)
    {
        throw new System.NotImplementedException();
    }

    public override async Task<TransactionResultDto> GetTransactionResultAsync(string transactionId)
    {
        return new() { State = TransactionState.Success };
    }
}