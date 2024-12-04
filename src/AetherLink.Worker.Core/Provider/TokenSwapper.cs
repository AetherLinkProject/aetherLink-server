using System;
using System.Threading.Tasks;
using AetherLink.Indexer.Dtos;
using AetherLink.Indexer.Provider;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Dtos;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface ITokenSwapper
{
    public Task<TokenAmountDto> ConstructSwapId(ReportContextDto reportContext, TokenAmountDto tokenAmount);
}

public class TokenSwapper : ITokenSwapper, ITransientDependency
{
    private readonly ILogger<TokenSwapper> _logger;
    private readonly IStorageProvider _storageProvider;
    private readonly IAeFinderProvider _aeFinderProvider;

    public TokenSwapper(IStorageProvider storageProvider, IAeFinderProvider aeFinderProvider,
        ILogger<TokenSwapper> logger)
    {
        _logger = logger;
        _storageProvider = storageProvider;
        _aeFinderProvider = aeFinderProvider;
    }

    public async Task<TokenAmountDto> ConstructSwapId(ReportContextDto reportContext, TokenAmountDto tokenAmount)
    {
        try
        {
            if (tokenAmount == null || string.IsNullOrEmpty(tokenAmount.TargetContractAddress))
            {
                _logger.LogWarning("[TokenSwapper] Get empty token amount");
                return null;
            }

            var tokenSwapConfigId = GenerateTokenSwapId(reportContext, tokenAmount);
            var tokenSwapConfig = await _storageProvider.GetAsync<TokenSwapConfigDto>(tokenSwapConfigId);
            
            // TODO: for test, will remove
            tokenSwapConfig = null;
            
            if (tokenSwapConfig == null)
            {
                _logger.LogDebug($"[TokenSwapper] Cannot find token swap config {tokenSwapConfigId} in local storage");

                var indexerConfig = await _aeFinderProvider.GetTokenSwapConfigAsync(tokenAmount.TargetChainId,
                    reportContext.SourceChainId, tokenAmount.TargetContractAddress, tokenAmount.TokenAddress,
                    tokenAmount.OriginToken);

                if (string.IsNullOrEmpty(indexerConfig?.TokenSwapConfig?.SwapId))
                {
                    _logger.LogDebug($"[TokenSwapper] Cannot find token swap config {tokenSwapConfigId} in indexer");
                    return tokenAmount;
                }

                tokenSwapConfig = indexerConfig.TokenSwapConfig;
                await _storageProvider.SetAsync(tokenSwapConfigId, tokenSwapConfig);
            }

            tokenAmount.SwapId = tokenSwapConfig.SwapId;
            if (string.IsNullOrEmpty(tokenAmount.OriginToken))
            {
                tokenAmount.OriginToken = tokenSwapConfig.OriginToken;
                _logger.LogDebug($"[TokenSwapper] need fill OriginToken: {tokenAmount.OriginToken}");
            }

            if (!string.IsNullOrEmpty(tokenAmount.TokenAddress)) return tokenAmount;

            tokenAmount.TokenAddress = tokenSwapConfig.TokenAddress;
            _logger.LogDebug($"[TokenSwapper] need fill TokenAddress: {tokenAmount.TokenAddress}");
            return tokenAmount;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[TokenSwapper]Get TokenSwapConfig failed.");
            return tokenAmount;
        }
    }

    private string GenerateTokenSwapId(ReportContextDto reportContext, TokenAmountDto data)
    {
        // CrossChain from aelf chain, TokenAddress is empty 
        var temp = !string.IsNullOrEmpty(data.TokenAddress) ? data.TokenAddress : data.OriginToken;
        return IdGeneratorHelper.GenerateId(data.TargetChainId, reportContext.SourceChainId,
            data.TargetContractAddress, temp, data.OriginToken);
    }
}