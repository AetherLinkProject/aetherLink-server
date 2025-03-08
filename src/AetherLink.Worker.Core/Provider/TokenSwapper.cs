using System;
using System.IO;
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
    public Task<TokenTransferMetadata> ConstructSwapId(ReportContextDto reportContext,
        TokenTransferMetadata tokenAmount);
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

    public async Task<TokenTransferMetadata> ConstructSwapId(ReportContextDto reportContext,
        TokenTransferMetadata tokenAmount)
    {
        try
        {
            if (tokenAmount == null || string.IsNullOrEmpty(reportContext.Receiver))
            {
                _logger.LogWarning("[TokenSwapper] Get empty token amount");
                return null;
            }

            var tokenSwapConfigId = GenerateTokenSwapId(reportContext, tokenAmount);
            // var tokenSwapConfig = await _storageProvider.GetAsync<TokenSwapConfigDto>(tokenSwapConfigId);
            // if (tokenSwapConfig == null)
            // {
            // todo for testnet debug
            _logger.LogDebug($"[TokenSwapper] Cannot find token swap config {tokenSwapConfigId} in local storage");

            var indexerConfig = await _aeFinderProvider.GetTokenSwapConfigAsync(tokenAmount.TargetChainId,
                reportContext.SourceChainId, reportContext.Receiver, tokenAmount.TokenAddress, tokenAmount.Symbol);

            if (string.IsNullOrEmpty(indexerConfig?.TokenSwapConfig?.SwapId))
            {
                _logger.LogDebug($"[TokenSwapper] Cannot find token swap config {tokenSwapConfigId} in indexer");
                throw new InvalidDataException("Could not find token swap config");
            }

            var tokenSwapConfig = indexerConfig.TokenSwapConfig;
            await _storageProvider.SetAsync(tokenSwapConfigId, tokenSwapConfig);
            // }

            tokenAmount.ExtraData = tokenSwapConfig.SwapId;
            if (string.IsNullOrEmpty(tokenAmount.Symbol))
            {
                tokenAmount.Symbol = tokenSwapConfig.OriginToken;
                _logger.LogDebug($"[TokenSwapper] need fill Symbol: {tokenAmount.Symbol}");
            }

            if (!string.IsNullOrEmpty(tokenAmount.TokenAddress)) return tokenAmount;

            tokenAmount.TokenAddress = tokenSwapConfig.TokenAddress;
            _logger.LogDebug($"[TokenSwapper] need fill TokenAddress: {tokenAmount.TokenAddress}");
            return tokenAmount;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[TokenSwapper]Get TokenSwapConfig failed.");
            throw;
        }
    }

    private string GenerateTokenSwapId(ReportContextDto reportContext, TokenTransferMetadata data)
    {
        // CrossChain from aelf chain, TokenAddress is empty 
        var temp = !string.IsNullOrEmpty(data.TokenAddress) ? data.TokenAddress : data.Symbol;
        return IdGeneratorHelper.GenerateId(data.TargetChainId, reportContext.SourceChainId,
            reportContext.Receiver, temp);
    }
}