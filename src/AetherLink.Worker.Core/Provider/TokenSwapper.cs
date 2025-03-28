using System;
using System.IO;
using System.Threading.Tasks;
using AElf;
using AetherLink.Indexer.Dtos;
using AetherLink.Indexer.Provider;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Nethereum.Util;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface ITokenSwapper
{
    public Task<TokenTransferMetadataDto> ConstructSwapId(ReportContextDto reportContext,
        TokenTransferMetadataDto tokenTransferMetadata);
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

    public async Task<TokenTransferMetadataDto> ConstructSwapId(ReportContextDto reportContext,
        TokenTransferMetadataDto tokenTransferMetadata)
    {
        try
        {
            if (tokenTransferMetadata == null)
            {
                _logger.LogWarning("[TokenSwapper] Get empty token amount");
                return null;
            }

            var tokenSwapConfigId = GenerateTokenSwapId(reportContext, tokenTransferMetadata);
            var tokenSwapConfig = await _storageProvider.GetAsync<TokenSwapConfigDto>(tokenSwapConfigId);
            // if (tokenSwapConfig == null)
            // {
            // todo for testnet debug
            _logger.LogDebug($"[TokenSwapper] Cannot find token swap config {tokenSwapConfigId} in local storage");

            var indexerConfig = await _aeFinderProvider.GetTokenSwapConfigAsync(tokenTransferMetadata.TargetChainId,
                reportContext.SourceChainId, reportContext.Receiver, tokenTransferMetadata.TokenAddress,
                tokenTransferMetadata.Symbol);

            if (string.IsNullOrEmpty(indexerConfig?.TokenSwapConfig?.ExtraData))
            {
                _logger.LogDebug($"[TokenSwapper] Cannot find token swap config {tokenSwapConfigId} in indexer");
                throw new InvalidDataException("Could not find token swap config");
            }

            tokenSwapConfig = indexerConfig.TokenSwapConfig;
            await _storageProvider.SetAsync(tokenSwapConfigId, tokenSwapConfig);
            // }

            tokenTransferMetadata.ExtraDataString = tokenSwapConfig.ExtraData;
            if (string.IsNullOrEmpty(tokenTransferMetadata.Symbol))
            {
                tokenTransferMetadata.Symbol = tokenSwapConfig.Symbol;
                _logger.LogDebug($"[TokenSwapper] need fill Symbol: {tokenSwapConfig.Symbol}");
            }

            if (!string.IsNullOrEmpty(tokenTransferMetadata.TokenAddress)) return tokenTransferMetadata;

            tokenTransferMetadata.TokenAddress = tokenSwapConfig.TokenAddress;
            _logger.LogDebug($"[TokenSwapper] need fill TokenAddress: {tokenSwapConfig.TokenAddress}");
            return tokenTransferMetadata;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[TokenSwapper]Get TokenSwapConfig failed.");
            throw;
        }
    }

    private string GenerateTokenSwapId(ReportContextDto reportContext, TokenTransferMetadataDto data)
    {
        // CrossChain from aelf chain, TokenAddress is empty 
        var temp = !string.IsNullOrEmpty(data.TokenAddress) ? data.TokenAddress : data.Symbol;
        return IdGeneratorHelper.GenerateId(data.TargetChainId, reportContext.SourceChainId, reportContext.Receiver,
            temp);
    }
}