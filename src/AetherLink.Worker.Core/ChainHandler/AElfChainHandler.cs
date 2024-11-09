using System.Collections.Generic;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.ChainHandler;

// Writer
public class AElfChainWriter : ChainWriter, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.AELF;

    public override async Task<string> SendCommitTransactionAsync(ReportContextDto reportContext,
        Dictionary<int, byte[]> signatures, CrossChainDataDto crossChainDat)
    {
        return "aelf-123";
    }
}

public class TDVVChainWriter : ChainWriter, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.TDVV;

    public override async Task<string> SendCommitTransactionAsync(ReportContextDto reportContext,
        Dictionary<int, byte[]> signatures, CrossChainDataDto crossChainDat)
    {
        return "tdvv-123";
    }
}

public class TDVWChainWriter : ChainWriter, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.TDVW;

    public override async Task<string> SendCommitTransactionAsync(ReportContextDto reportContext,
        Dictionary<int, byte[]> signatures, CrossChainDataDto crossChainDat)
    {
        return "tdvw-123";
    }
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