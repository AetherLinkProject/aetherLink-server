using System;
using System.Collections.Concurrent;
using AElf;
using AElf.Types;
using System.Threading.Tasks;
using AetherLink.Contracts.Oracle;
using AetherLink.Worker.Core.Common;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Oracle;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IOracleContractProvider
{
    public Task<long> GetOracleLatestEpochAndRoundAsync(string chainId);
    public Task<Commitment> GetRequestCommitmentAsync(string chainId, string transactionId, string requestId);
    public Task<long> GetStartEpochAsync(string chainId, long blockHeight);

    public Task<TransmitInput> GenerateTransmitDataAsync(string chainId, string requestId, string transactionId,
        long epoch, ByteString result);
}

// If there is request-related data loss in Indexer, the transaction results and contract view method will be used as a backup.
public class OracleContractProvider : IOracleContractProvider, ISingletonDependency
{
    private readonly IIndexerProvider _indexerProvider;
    private readonly IContractProvider _contractProvider;
    private readonly ILogger<OracleContractProvider> _logger;
    private readonly ConcurrentDictionary<string, Commitment> _commitmentsCache = new();

    public OracleContractProvider(IIndexerProvider indexerProvider, IContractProvider contractProvider,
        ILogger<OracleContractProvider> logger)
    {
        _logger = logger;
        _indexerProvider = indexerProvider;
        _contractProvider = contractProvider;
    }

    public async Task<long> GetOracleLatestEpochAndRoundAsync(string chainId)
    {
        var epoch = await _indexerProvider.GetOracleLatestEpochAsync(chainId, 0);
        if (epoch > 0)
        {
            _logger.LogDebug("[OracleContractProvider] Get indexer Latest epoch :{epoch}", epoch);
            return epoch;
        }

        var latestR = await _contractProvider.GetLatestRoundAsync(chainId);
        return latestR.Value;
    }

    public async Task<Commitment> GetRequestCommitmentAsync(string chainId, string transactionId, string requestId)
    {
        var commitmentId = IdGeneratorHelper.GenerateId(chainId, requestId);
        if (_commitmentsCache.TryGetValue(commitmentId, out var commitmentCache))
        {
            return commitmentCache;
        }

        var commitmentStr = await _indexerProvider.GetRequestCommitmentAsync(chainId, requestId);
        if (!string.IsNullOrEmpty(commitmentStr))
        {
            _logger.LogDebug("[OracleContractProvider] Get indexer Commitment:{commitment}", commitmentStr);
            _commitmentsCache[commitmentId] = Commitment.Parser.ParseFrom(ByteString.FromBase64(commitmentStr));
        }
        else
        {
            _commitmentsCache[commitmentId] = await _contractProvider.GetCommitmentAsync(chainId, transactionId);
        }

        return _commitmentsCache[commitmentId];
    }

    public async Task<long> GetStartEpochAsync(string chainId, long blockHeight)
    {
        try
        {
            var epoch = await _indexerProvider.GetOracleLatestEpochAsync(chainId, blockHeight);

            if (epoch > 0)
            {
                _logger.LogDebug(
                    "[OracleContractProvider] Get indexer request start epoch:{epoch} before blockHeight:{height}",
                    epoch, blockHeight);
                return epoch;
            }

            var latestR = await _contractProvider.GetLatestRoundAsync(chainId);
            return latestR.Value;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("[OracleContractProvider] Get {chain} start epoch timeout", chainId);
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[OracleContractProvider] Get {chain} start epoch failed", chainId);
            throw;
        }
    }

    public async Task<TransmitInput> GenerateTransmitDataAsync(string chainId, string requestId, string transactionId,
        long epoch, ByteString result)
    {
        var commitment = await GetRequestCommitmentAsync(chainId, transactionId, requestId);
        var transmitData = new TransmitInput
        {
            Report = new Report
            {
                Result = result,
                OnChainMetadata = commitment.ToByteString(),
                Error = ByteString.Empty,
                OffChainMetadata = ByteString.Empty
            }.ToByteString()
        };

        transmitData.ReportContext.Add(await GetOracleConfigAsync(chainId));
        transmitData.ReportContext.Add(HashHelper.ComputeFrom(epoch));
        transmitData.ReportContext.Add(HashHelper.ComputeFrom(0));
        return transmitData;
    }

    private async Task<Hash> GetOracleConfigAsync(string chainId)
    {
        var configStr = await _indexerProvider.GetOracleConfigAsync(chainId);
        if (!string.IsNullOrEmpty(configStr))
        {
            _logger.LogDebug("[OracleContractProvider] Get indexer Oracle config :{config}", configStr);
            return Hash.LoadFromHex(configStr);
        }

        var config = await _contractProvider.GetOracleConfigAsync(chainId);
        return config.Config.LatestConfigDigest;
    }
}