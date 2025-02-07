using System.Collections.Generic;
using System.Threading.Tasks;
using AElf;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Provider;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.ChainHandler;

// Writer
public class EvmChainWriter : ChainWriter, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.EVM;
    private readonly IContractProvider _contractProvider;

    public override Task<string> SendCommitTransactionAsync(ReportContextDto reportContext,
        Dictionary<int, byte[]> signatures, CrossChainDataDto crossChainDat)
    {
        // var tx = await PerformSendTransactionAsync("SetGasPrice", input, chainId);
        // return new SendTransactionResult
        // {
        //     Transaction = tx,
        //     TransactionResult = await PerformGetTransactionResultAsync(tx.GetHash().ToHex(), chainId)
        // };
        throw new System.NotImplementedException();
    }
}

// Reader
public class EvmChainReader : ChainReader, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.EVM;
    private readonly IContractProvider _contractProvider;

    public EvmChainReader(IContractProvider contractProvider)
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