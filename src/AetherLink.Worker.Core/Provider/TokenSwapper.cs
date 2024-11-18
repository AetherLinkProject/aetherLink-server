using System.Threading.Tasks;
using AElf;
using AetherLink.Contracts.Ramp;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Dtos;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface ITokenSwapper
{
    public Task<TokenAmountDto> ConstructSwapId(TokenAmountDto tokenAmount);
}

public class TokenSwapper : ITokenSwapper, ITransientDependency
{
    private readonly ILogger<TokenSwapper> _logger;
    private readonly IStorageProvider _storageProvider;
    private readonly IAeFinderProvider _aeFinderProvider;
    private readonly IContractProvider _contractProvider;

    public TokenSwapper(IStorageProvider storageProvider, ILogger<TokenSwapper> logger,
        IAeFinderProvider aeFinderProvider, IContractProvider contractProvider)
    {
        _logger = logger;
        _storageProvider = storageProvider;
        _aeFinderProvider = aeFinderProvider;
        _contractProvider = contractProvider;
    }

    public async Task<TokenAmountDto> ConstructSwapId(TokenAmountDto tokenAmount)
    {
        if (tokenAmount == null) return null;
        tokenAmount.SwapId = await GetTokenSwapConfigAsync(tokenAmount);
        return tokenAmount;
    }

    private async Task<string> GetTokenSwapConfigAsync(TokenAmountDto tokenAmount)
    {
        var tokenSwapConfigId = GenerateTokenSwapId(tokenAmount);
        var tokenSwapConfig = await _storageProvider.GetAsync<TokenAmountDto>(tokenSwapConfigId);
        if (tokenSwapConfig != null)
        {
            return tokenSwapConfig.SwapId;
        }

        _logger.LogDebug($"[TokenSwapper] Cannot find token swap config {tokenSwapConfigId} in local storage");

        var indexerConfig = await _aeFinderProvider.GetTokenSwapConfigAsync(tokenAmount.TargetChainId,
            tokenAmount.TargetContractAddress, tokenAmount.TokenAddress, tokenAmount.OriginToken);

        if (!string.IsNullOrEmpty(indexerConfig.SwapId))
        {
            return indexerConfig.SwapId;
        }

        _logger.LogDebug($"[TokenSwapper] Cannot find token swap config {tokenSwapConfigId} in indexer");

        var contractConfigId = HashHelper.ComputeFrom(new TokenSwapInfo
        {
            TargetChainId = tokenAmount.TargetChainId,
            TargetContractAddress = tokenAmount.TargetContractAddress,
            OriginToken = tokenAmount.OriginToken,
            TokenAddress = tokenAmount.TokenAddress
        });

        return await _contractProvider.QueryTokenSwapConfigOnChainAsync(contractConfigId);
    }

    private string GenerateTokenSwapId(TokenAmountDto data) => IdGeneratorHelper.GenerateId(data.TargetChainId,
        data.TargetContractAddress, data.TokenAddress, data.OriginToken);
}