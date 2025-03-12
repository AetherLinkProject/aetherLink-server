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
            EvmHelper.GenerateTokenTransferMetadataBytes(crossChainData.TokenTransferMetadataDto);
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
        var tokenTransferMetadata = EvmHelper.GenerateTokenTransferMetadataBytes(crossChainData.TokenTransferMetadataDto);
        var (rs, ss, rawVs) = EvmHelper.AggregateSignatures(signatures.Values.ToList());
        return await _evmContractProvider.TransmitAsync(_evmOptions, contextBytes, messageBytes, tokenTransferMetadata, rs, ss,
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
        var tokenTransferMetadata = EvmHelper.GenerateTokenTransferMetadataBytes(crossChainData.TokenTransferMetadataDto);
        var (rs, ss, rawVs) = EvmHelper.AggregateSignatures(signatures.Values.ToList());
        return await _evmContractProvider.TransmitAsync(_evmOptions, contextBytes, messageBytes, tokenTransferMetadata, rs, ss,
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
        var tokenTransferMetadata = EvmHelper.GenerateTokenTransferMetadataBytes(crossChainData.TokenTransferMetadataDto);
        var (rs, ss, rawVs) = EvmHelper.AggregateSignatures(signatures.Values.ToList());
        return await _evmContractProvider.TransmitAsync(_evmOptions, contextBytes, messageBytes, tokenTransferMetadata, rs, ss,
            rawVs);
    }
}

// Reader
public class EvmChainReader : ChainReader, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.EVM;
    private readonly IEvmProvider _evmProvider;
    private readonly EvmOptions _evmOptions;

    public EvmChainReader(IEvmProvider evmContractProvider, IOptionsSnapshot<EvmContractsOptions> evmOptions)
    {
        _evmProvider = evmContractProvider;
        _evmOptions = EvmHelper.GetEvmContractConfig(ChainId, evmOptions.Value);
    }

    public override Task<byte[]> CallTransactionAsync(byte[] transaction)
    {
        throw new System.NotImplementedException();
    }

    public override async Task<TransactionResultDto> GetTransactionResultAsync(string transactionId) => new()
        { State = await _evmProvider.GetTransactionResultAsync(_evmOptions, transactionId) };

    public override string ConvertBytesToAddressStr(byte[] addressBytes)
    {
        return AElf.Types.Address.FromBytes(addressBytes).ToBase58();
    }
}

public class BscChainReader : ChainReader, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.BSC;
    private readonly IEvmProvider _evmProvider;
    private readonly EvmOptions _evmOptions;

    public BscChainReader(IEvmProvider evmContractProvider, IOptionsSnapshot<EvmContractsOptions> evmOptions)
    {
        _evmProvider = evmContractProvider;
        _evmOptions = EvmHelper.GetEvmContractConfig(ChainId, evmOptions.Value);
    }

    public override Task<byte[]> CallTransactionAsync(byte[] transaction)
    {
        throw new System.NotImplementedException();
    }

    public override async Task<TransactionResultDto> GetTransactionResultAsync(string transactionId) => new()
        { State = await _evmProvider.GetTransactionResultAsync(_evmOptions, transactionId) };

    public override string ConvertBytesToAddressStr(byte[] addressBytes)
    {
        return AElf.Types.Address.FromBytes(addressBytes).ToBase58();
    }
}

public class BscTestChainReader : ChainReader, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.BSCTEST;
    private readonly IEvmProvider _evmProvider;
    private readonly EvmOptions _evmOptions;

    public BscTestChainReader(IEvmProvider evmContractProvider, IOptionsSnapshot<EvmContractsOptions> evmOptions)
    {
        _evmProvider = evmContractProvider;
        _evmOptions = EvmHelper.GetEvmContractConfig(ChainId, evmOptions.Value);
    }
    public override Task<byte[]> CallTransactionAsync(byte[] transaction)
    {
        throw new System.NotImplementedException();
    }

    public override async Task<TransactionResultDto> GetTransactionResultAsync(string transactionId) => new()
        { State = await _evmProvider.GetTransactionResultAsync(_evmOptions, transactionId) };

    public override string ConvertBytesToAddressStr(byte[] addressBytes)
    {
        return AElf.Types.Address.FromBytes(addressBytes).ToBase58();
    }
}

public class SEPOLIAChainReader : ChainReader, ISingletonDependency
{
    public override long ChainId => ChainIdConstants.SEPOLIA;
    private readonly IEvmProvider _evmProvider;
    private readonly EvmOptions _evmOptions;

    public SEPOLIAChainReader(IEvmProvider evmContractProvider, IOptionsSnapshot<EvmContractsOptions> evmOptions)
    {
        _evmProvider = evmContractProvider;
        _evmOptions = EvmHelper.GetEvmContractConfig(ChainId, evmOptions.Value);
    }

    public override Task<byte[]> CallTransactionAsync(byte[] transaction)
    {
        throw new System.NotImplementedException();
    }

    public override async Task<TransactionResultDto> GetTransactionResultAsync(string transactionId) => new()
        { State = await _evmProvider.GetTransactionResultAsync(_evmOptions, transactionId) };

    public override string ConvertBytesToAddressStr(byte[] addressBytes)
    {
        return AElf.Types.Address.FromBytes(addressBytes).ToBase58();
    }
}