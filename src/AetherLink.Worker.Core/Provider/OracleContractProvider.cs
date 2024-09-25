using System;
using System.Collections.Concurrent;
using AElf;
using AElf.Types;
using System.Threading.Tasks;
using AetherLink.Contracts.Oracle;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Dtos;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Oracle;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IOracleContractProvider
{
    public Task<long> GetOracleLatestEpochAndRoundAsync(string chainId);
    public Task<Commitment> GetRequestCommitmentAsync(string chainId, string requestId);
    public Task<Commitment> GetRequestCommitmentByTxAsync(string chainId, string transactionId);
    public Task<long> GetStartEpochAsync(string chainId, long blockHeight);
    public Task<Report> GetTransmitReportByTransactionIdAsync(string chainId, string transactionId);

    public Task<TransmitInput> GenerateTransmitDataAsync(string chainId, string requestId, long epoch,
        ByteString result);

    public Task<TransmitInput> GenerateTransmitDataByTransactionIdAsync(string chainId, string transactionId,
        long epoch, ByteString result);
}

// If there is request-related data loss in Indexer, the transaction results and contract view method will be used as a backup.
public class OracleContractProvider : IOracleContractProvider, ISingletonDependency
{
    private readonly IAeFinderProvider _aeFinderProvider;
    private readonly IContractProvider _contractProvider;
    private readonly ILogger<OracleContractProvider> _logger;
    private readonly ConcurrentDictionary<string, Commitment> _commitmentsCache = new();

    public OracleContractProvider(AeFinderProvider aeFinderProvider, IContractProvider contractProvider,
        ILogger<OracleContractProvider> logger)
    {
        _logger = logger;
        _aeFinderProvider = aeFinderProvider;
        _contractProvider = contractProvider;
    }

    public async Task<long> GetOracleLatestEpochAndRoundAsync(string chainId)
    {
        var epoch = await _aeFinderProvider.GetOracleLatestEpochAsync(chainId, 0);
        if (epoch > 0)
        {
            _logger.LogDebug("[OracleContract] Get indexer Latest epoch: {epoch}", epoch);
            return epoch;
        }

        var latestR = await _contractProvider.GetLatestRoundAsync(chainId);
        return latestR.Value;
    }

    public async Task<Commitment> GetRequestCommitmentAsync(string chainId, string requestId)
    {
        var commitmentId = IdGeneratorHelper.GenerateId(chainId, requestId);
        if (_commitmentsCache.TryGetValue(commitmentId, out var commitmentCache)) return commitmentCache;

        var commitmentStr = await _aeFinderProvider.GetRequestCommitmentAsync(chainId, requestId);
        _logger.LogDebug("[OracleContract] Get indexer Commitment:{commitment}", commitmentStr);

        _commitmentsCache[commitmentId] = Commitment.Parser.ParseFrom(ByteString.FromBase64(commitmentStr));
        return _commitmentsCache[commitmentId];
    }

    public async Task<Commitment> GetRequestCommitmentByTxAsync(string chainId, string transactionId)
    {
        var commitment = await _contractProvider.GetCommitmentAsync(chainId, transactionId);
        var commitmentId = IdGeneratorHelper.GenerateId(chainId, commitment.RequestId);
        if (!_commitmentsCache.TryGetValue(commitmentId, out _)) _commitmentsCache[commitmentId] = commitment;

        return commitment;
    }

    public async Task<long> GetStartEpochAsync(string chainId, long blockHeight)
    {
        try
        {
            var epoch = await _aeFinderProvider.GetOracleLatestEpochAsync(chainId, blockHeight);

            if (epoch <= 0) return (await _contractProvider.GetLatestRoundAsync(chainId)).Value;
            _logger.LogDebug("[OracleContract] Get indexer request start epoch:{epoch} before :{height}", epoch,
                blockHeight);
            return epoch;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("[OracleContract] Get {chain} start epoch timeout", chainId);
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[OracleContract] Get {chain} start epoch failed", chainId);
            throw;
        }
    }

    public async Task<TransmitInput> GenerateTransmitDataAsync(string chainId, string requestId, long epoch,
        ByteString result)
    {
        var transmitData = new TransmitInput
        {
            Report = new Report
            {
                Result = result,
                OnChainMetadata = (await GetRequestCommitmentAsync(chainId, requestId)).ToByteString(),
                Error = ByteString.Empty,
                OffChainMetadata = ByteString.Empty
            }.ToByteString()
        };

        transmitData.ReportContext.Add(await GetOracleConfigAsync(chainId));
        transmitData.ReportContext.Add(HashHelper.ComputeFrom(epoch));
        transmitData.ReportContext.Add(HashHelper.ComputeFrom(0));
        return transmitData;
    }

    public async Task<TransmitInput> GenerateTransmitDataByTransactionIdAsync(string chainId, string transactionId,
        long epoch, ByteString result)
    {
        var transmitData = new TransmitInput
        {
            Report = new Report
            {
                Result = result,
                OnChainMetadata = (await GetRequestCommitmentByTxAsync(chainId, transactionId)).ToByteString(),
                Error = ByteString.Empty,
                OffChainMetadata = ByteString.Empty
            }.ToByteString()
        };

        transmitData.ReportContext.Add(await GetOracleConfigAsync(chainId));
        transmitData.ReportContext.Add(HashHelper.ComputeFrom(epoch));
        transmitData.ReportContext.Add(HashHelper.ComputeFrom(0));
        return transmitData;
    }

    public async Task<TransmitInputDto> GetTransmitInputByTransactionIdAsync(string chainId, string transactionId)
        => JsonConvert.DeserializeObject<TransmitInputDto>(
            (await _contractProvider.GetTxResultAsync(chainId, transactionId)).Transaction.Params);

    public async Task<Report> GetTransmitReportByTransactionIdAsync(string chainId, string transactionId)
    {
        var txResult = await _contractProvider.GetTxResultAsync(chainId, transactionId);
        var input = JsonConvert.DeserializeObject<TransmitInputDto>(txResult.Transaction.Params);
        return Report.Parser.ParseFrom(ByteString.FromBase64(input.Report));
    }

    private async Task<Hash> GetOracleConfigAsync(string chainId)
    {
        var configStr = await _aeFinderProvider.GetOracleConfigAsync(chainId);
        if (!string.IsNullOrEmpty(configStr))
        {
            _logger.LogDebug("[OracleContractProvider] Get indexer Oracle config :{config}", configStr);
            return Hash.LoadFromHex(configStr);
        }

        var config = await _contractProvider.GetOracleConfigAsync(chainId);
        return config.Config.LatestConfigDigest;
    }
}