using System;
using System.Threading.Tasks;
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

    public TokenSwapper(IStorageProvider storageProvider, ILogger<TokenSwapper> logger,
        IAeFinderProvider aeFinderProvider)
    {
        _logger = logger;
        _storageProvider = storageProvider;
        _aeFinderProvider = aeFinderProvider;
    }

    public async Task<TokenAmountDto> ConstructSwapId(TokenAmountDto tokenAmount)
    {
        if (tokenAmount == null) return null;
        tokenAmount.SwapId = await GetTokenSwapConfigAsync(tokenAmount);
        return tokenAmount;
    }

    private async Task<string> GetTokenSwapConfigAsync(TokenAmountDto tokenAmount)
    {
        try
        {
            var tokenSwapConfigId = GenerateTokenSwapId(tokenAmount);
            var tokenSwapConfig = await _storageProvider.GetAsync<TokenAmountDto>(tokenSwapConfigId);
            if (tokenSwapConfig != null) return tokenSwapConfig.SwapId;

            _logger.LogDebug($"[TokenSwapper] Cannot find token swap config {tokenSwapConfigId} in local storage");

            var indexerConfig = await _aeFinderProvider.GetTokenSwapConfigAsync(tokenAmount.TargetChainId,
                tokenAmount.TargetContractAddress, tokenAmount.TokenAddress, tokenAmount.OriginToken);

            return indexerConfig.TokenSwapConfig.SwapId;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[TokenSwapper]Get TokenSwapConfig failed.");
            return "";
        }
    }

    private string GenerateTokenSwapId(TokenAmountDto data) => IdGeneratorHelper.GenerateId(data.TargetChainId,
        data.TargetContractAddress, data.TokenAddress, data.OriginToken);
}