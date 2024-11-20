using System;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Dtos;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.Provider;

public interface ITokenSwapper
{
    public Task<TokenAmountDto> ConstructSwapId(TokenAmountDto tokenAmount);
}

public class TokenSwapper : ITokenSwapper, ITransientDependency
{
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<TokenSwapper> _logger;
    private readonly IStorageProvider _storageProvider;
    private readonly IAeFinderProvider _aeFinderProvider;

    public TokenSwapper(IStorageProvider storageProvider, ILogger<TokenSwapper> logger,
        IAeFinderProvider aeFinderProvider, IObjectMapper objectMapper)
    {
        _logger = logger;
        _objectMapper = objectMapper;
        _storageProvider = storageProvider;
        _aeFinderProvider = aeFinderProvider;
    }

    public async Task<TokenAmountDto> ConstructSwapId(TokenAmountDto tokenAmount)
    {
        try
        {
            if (tokenAmount == null)
            {
                _logger.LogWarning("[TokenSwapper] Get empty token amount");
                return null;
            }
            
            var tokenSwapConfigId = GenerateTokenSwapId(tokenAmount);
            var tokenSwapConfig = await _storageProvider.GetAsync<TokenAmountDto>(tokenSwapConfigId);
            if (tokenSwapConfig != null) return tokenSwapConfig;

            _logger.LogDebug($"[TokenSwapper] Cannot find token swap config {tokenSwapConfigId} in local storage");

            var indexerConfig = await _aeFinderProvider.GetTokenSwapConfigAsync(tokenAmount.TargetChainId,
                tokenAmount.TargetContractAddress, tokenAmount.TokenAddress, tokenAmount.OriginToken);

            return _objectMapper.Map<TokenSwapConfigDto, TokenAmountDto>(indexerConfig.TokenSwapConfig);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[TokenSwapper]Get TokenSwapConfig failed.");
            return tokenAmount;
        }
    }

    private string GenerateTokenSwapId(TokenAmountDto data)
    {
        if (!string.IsNullOrEmpty(data.TokenAddress))
        {
            return IdGeneratorHelper.GenerateId(data.TargetChainId,
                data.TargetContractAddress, data.TokenAddress);
        }

        if (!string.IsNullOrEmpty(data.OriginToken))
        {
            return IdGeneratorHelper.GenerateId(data.TargetChainId,
                data.TargetContractAddress, data.OriginToken);
        }

        return IdGeneratorHelper.GenerateId(data.TargetChainId,
            data.TargetContractAddress, data.TokenAddress, data.OriginToken);
    }
}