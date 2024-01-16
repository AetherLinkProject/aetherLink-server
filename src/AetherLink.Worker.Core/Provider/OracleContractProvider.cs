using System.Threading.Tasks;
using AElf.Types;
using AetherLink.Worker.Core.Common.ContractHandler;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oracle;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IOracleContractProvider
{
    public Task<Hash> GetOracleConfigAsync(string chainId);
    public Task<long> GetLatestRoundAsync(string chainId);
    public Task<Commitment> GetCommitmentAsync(string chainId, string transactionId, string requestId);
}

public class OracleContractProvider : IOracleContractProvider, ISingletonDependency
{
    private readonly IIndexerProvider _indexerProvider;
    private readonly IContractProvider _contractProvider;
    private readonly ILogger<OracleContractProvider> _logger;

    public OracleContractProvider(IIndexerProvider indexerProvider, IContractProvider contractProvider,
        ILogger<OracleContractProvider> logger)
    {
        _logger = logger;
        _indexerProvider = indexerProvider;
        _contractProvider = contractProvider;
    }

    public async Task<Hash> GetOracleConfigAsync(string chainId)
    {
        var configStr = await _indexerProvider.GetOracleConfigAsync(chainId);
        _logger.LogInformation("[OracleContractProvider] Get indexer Oracle config :{config}", configStr);

        if (!string.IsNullOrEmpty(configStr)) return Hash.LoadFromHex(configStr);

        var config = await _contractProvider.GetOracleConfigAsync(chainId);
        return config.Config.LatestConfigDigest;
    }

    public async Task<long> GetLatestRoundAsync(string chainId)
    {
        var latestRound = await _indexerProvider.GetLatestRoundAsync(chainId);
        _logger.LogInformation("[OracleContractProvider] Get indexer Latest round :{epoch}", latestRound);

        if (latestRound > 0) return latestRound;

        var latestR = await _contractProvider.GetLatestRoundAsync(chainId);
        return latestR.Value;
    }

    public async Task<Commitment> GetCommitmentAsync(string chainId, string transactionId, string requestId)
    {
        var commitment = await _indexerProvider.GetCommitmentAsync(chainId, requestId);
        _logger.LogInformation("[OracleContractProvider] Get indexer Commitment:{commitment}", commitment);

        if (!string.IsNullOrEmpty(commitment))
            return Commitment.Parser.ParseFrom(ByteString.FromBase64(commitment));

        return await _contractProvider.GetCommitmentAsync(chainId, transactionId);
    }
}