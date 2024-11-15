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

    public TokenSwapper(IStorageProvider storageProvider, ILogger<TokenSwapper> logger)
    {
        _logger = logger;
        _storageProvider = storageProvider;
    }

    public async Task<TokenAmountDto> ConstructSwapId(TokenAmountDto tokenAmount)
    {
        tokenAmount.SwapId = await GetTokenSwapConfigAsync(tokenAmount);
        return tokenAmount;
    }

    private async Task<string> GetTokenSwapConfigAsync(TokenAmountDto tokenAmount)
    {
        var tokenSwapConfigId = GenerateTokenSwapId(tokenAmount);
        var tokenSwapConfig = await _storageProvider.GetAsync<TokenAmountDto>(tokenSwapConfigId);
        if (tokenSwapConfig == null)
        {
            _logger.LogDebug($"[TokenSwapper] Cannot find token swap config {tokenSwapConfigId} in local storage");
        }

        // todo find it in indexer

        // todo find it in contract

        return "test_swap_id";
    }

    private string GenerateTokenSwapId(TokenAmountDto data) => IdGeneratorHelper.GenerateId(data.TargetChainId,
        data.TargetContractAddress, data.TokenAddress, data.OriginToken);
}