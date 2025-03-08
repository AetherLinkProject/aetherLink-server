using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.ChainHandler;

// Writer
public class EvmChainWriter : ChainWriter, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.EVM;
    private readonly IEvmProvider _evmContractProvider;
    private readonly EvmOptions _evmOptions;

    public EvmChainWriter(IEvmProvider evmContractProvider, IOptionsSnapshot<EvmContractsOptions> evmOptions)
    {
        _evmContractProvider = evmContractProvider;
        _evmOptions = EvmHelper.GetEvmContractConfig(ChainId, evmOptions.Value);
    }

    public override async Task<string> SendCommitTransactionAsync(ReportContextDto reportContext,
        Dictionary<int, byte[]> signatures, CrossChainDataDto crossChainData)
    {
        var contextBytes = EvmHelper.GenerateReportContextBytes(reportContext);
        var messageBytes = EvmHelper.GenerateMessageBytes(crossChainData.Message);
        var tokenTransferMetadataBytes =
            EvmHelper.GenerateTokenTransferMetadataBytes(crossChainData.TokenTransferMetadata);
        var (rs, ss, rawVs) = EvmHelper.AggregateSignatures(signatures.Values.ToList());
        return await _evmContractProvider.TransmitAsync(_evmOptions, contextBytes, messageBytes,
            tokenTransferMetadataBytes, rs, ss, rawVs);
    }
}

public class BscChainWriter : ChainWriter, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.BSC;
    private readonly IEvmProvider _evmContractProvider;
    private readonly EvmOptions _evmOptions;

    public BscChainWriter(IEvmProvider evmContractProvider, IOptionsSnapshot<EvmContractsOptions> evmOptions)
    {
        _evmContractProvider = evmContractProvider;
        _evmOptions = EvmHelper.GetEvmContractConfig(ChainId, evmOptions.Value);
    }

    public override async Task<string> SendCommitTransactionAsync(ReportContextDto reportContext,
        Dictionary<int, byte[]> signatures, CrossChainDataDto crossChainData)
    {
        var contextBytes = EvmHelper.GenerateReportContextBytes(reportContext);
        var messageBytes = EvmHelper.GenerateMessageBytes(crossChainData.Message);
        var tokenAmount = EvmHelper.GenerateTokenTransferMetadataBytes(crossChainData.TokenTransferMetadata);
        var (rs, ss, rawVs) = EvmHelper.AggregateSignatures(signatures.Values.ToList());
        return await _evmContractProvider.TransmitAsync(_evmOptions, contextBytes, messageBytes, tokenAmount, rs, ss,
            rawVs);
    }
}

public class SEPOLIAChainWriter : ChainWriter, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.SEPOLIA;
    private readonly IEvmProvider _evmContractProvider;
    private readonly EvmOptions _evmOptions;

    public SEPOLIAChainWriter(IEvmProvider evmContractProvider, IOptionsSnapshot<EvmContractsOptions> evmOptions)
    {
        _evmContractProvider = evmContractProvider;
        _evmOptions = EvmHelper.GetEvmContractConfig(ChainId, evmOptions.Value);
    }

    public override async Task<string> SendCommitTransactionAsync(ReportContextDto reportContext,
        Dictionary<int, byte[]> signatures, CrossChainDataDto crossChainData)
    {
        var contextBytes = EvmHelper.GenerateReportContextBytes(reportContext);
        var messageBytes = EvmHelper.GenerateMessageBytes(crossChainData.Message);
        var tokenAmount = EvmHelper.GenerateTokenTransferMetadataBytes(crossChainData.TokenTransferMetadata);
        var (rs, ss, rawVs) = EvmHelper.AggregateSignatures(signatures.Values.ToList());
        return await _evmContractProvider.TransmitAsync(_evmOptions, contextBytes, messageBytes, tokenAmount, rs, ss,
            rawVs);
    }
}

public class BscTestChainWriter : ChainWriter, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.BSCTEST;
    private readonly IEvmProvider _evmContractProvider;
    private readonly EvmOptions _evmOptions;

    public BscTestChainWriter(IEvmProvider evmContractProvider, IOptionsSnapshot<EvmContractsOptions> evmOptions)
    {
        _evmContractProvider = evmContractProvider;
        _evmOptions = EvmHelper.GetEvmContractConfig(ChainId, evmOptions.Value);
    }

    public override async Task<string> SendCommitTransactionAsync(ReportContextDto reportContext,
        Dictionary<int, byte[]> signatures, CrossChainDataDto crossChainData)
    {
        var contextBytes = EvmHelper.GenerateReportContextBytes(reportContext);
        var messageBytes = EvmHelper.GenerateMessageBytes(crossChainData.Message);
        var tokenAmount = EvmHelper.GenerateTokenTransferMetadataBytes(crossChainData.TokenTransferMetadata);
        var (rs, ss, rawVs) = EvmHelper.AggregateSignatures(signatures.Values.ToList());
        return await _evmContractProvider.TransmitAsync(_evmOptions, contextBytes, messageBytes, tokenAmount, rs, ss,
            rawVs);
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

public class BscChainReader : ChainReader, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.BSC;
    private readonly IContractProvider _contractProvider;

    public BscChainReader(IContractProvider contractProvider)
    {
        _contractProvider = contractProvider;
    }

    public override Task<byte[]> CallTransactionAsync(byte[] transaction)
    {
        throw new System.NotImplementedException();
    }

    public override async Task<TransactionResultDto> GetTransactionResultAsync(string transactionId)
        => new() { State = TransactionState.Success };

    public override string ConvertBytesToAddressStr(byte[] addressBytes)
    {
        return AElf.Types.Address.FromBytes(addressBytes).ToBase58();
    }
}

public class BscTestChainReader : ChainReader, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.BSCTEST;
    private readonly IContractProvider _contractProvider;

    public BscTestChainReader(IContractProvider contractProvider)
    {
        _contractProvider = contractProvider;
    }

    public override Task<byte[]> CallTransactionAsync(byte[] transaction)
    {
        throw new System.NotImplementedException();
    }

    public override async Task<TransactionResultDto> GetTransactionResultAsync(string transactionId)
        => new() { State = TransactionState.Success };

    public override string ConvertBytesToAddressStr(byte[] addressBytes)
    {
        return AElf.Types.Address.FromBytes(addressBytes).ToBase58();
    }
}

public class SEPOLIAChainReader : ChainReader, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.SEPOLIA;
    private readonly IContractProvider _contractProvider;

    public SEPOLIAChainReader(IContractProvider contractProvider)
    {
        _contractProvider = contractProvider;
    }

    public override Task<byte[]> CallTransactionAsync(byte[] transaction)
    {
        throw new System.NotImplementedException();
    }

    public override async Task<TransactionResultDto> GetTransactionResultAsync(string transactionId)
        => new() { State = TransactionState.Success };

    public override string ConvertBytesToAddressStr(byte[] addressBytes)
    {
        return AElf.Types.Address.FromBytes(addressBytes).ToBase58();
    }
}