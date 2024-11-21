using AetherLink.Indexer.Dtos;
using AetherLink.Server.Grains.State;
using Microsoft.Extensions.Logging;

namespace AetherLink.Server.Grains.Grain.Indexer;

public interface IAELFBlockHeightGrain : IGrainWithStringKey
{
    Task UpdateHeightAsync(List<ChainItemDto> chainItems);
    Task<List<ChainItemDto>> GetBlockHeightAsync();
    Task UpdateConsumedHeightAsync(string chainId, long blockHeight);
    Task<long> GetConsumedHeightAsync(string chainId);
}

public class AELFBlockHeightGrain : Grain<AELFBlockHeightState>, IAELFBlockHeightGrain
{
    // private readonly IContractProvider _contractProvider;
    private readonly ILogger<AELFBlockHeightGrain> _logger;

    public AELFBlockHeightGrain(ILogger<AELFBlockHeightGrain> logger)
    {
        _logger = logger;
    }

    // public async Task<long> UpdateSideChainIndexHeightAsync(string targetChainId, string sourceChainId)
    // {
    //     State.SideChainBlockHeight = await _contractProvider.GetSideChainIndexHeightAsync(targetChainId, sourceChainId);
    //     await WriteStateAsync();
    //
    //     _logger.LogInformation("Updated side chain index height to {height}", State.SideChainBlockHeight);
    //
    //     return State.SideChainBlockHeight;
    // }
    //
    //
    // public async Task<long> UpdateMainChainIndexHeightAsync(string sourceChainId)
    // {
    //     State.MainChainBlockHeight = await _contractProvider.GetIndexHeightAsync(sourceChainId);
    //     await WriteStateAsync();
    //
    //     _logger.LogInformation("Updated main chain index height to {height}", State.MainChainBlockHeight);
    //
    //     return State.MainChainBlockHeight;
    // }
    //
    // public Task<long> GetSideChainIndexHeightAsync() => Task.FromResult(State.SideChainBlockHeight);
    // public Task<long> GetMainChainIndexHeightAsync() => Task.FromResult(State.MainChainBlockHeight);

    public async Task UpdateHeightAsync(List<ChainItemDto> chainItems)
    {
        State.ChainItems = chainItems;
        await WriteStateAsync();
    }

    public Task<List<ChainItemDto>> GetBlockHeightAsync()
    {
        throw new NotImplementedException();
    }

    public Task UpdateConsumedHeightAsync(string chainId, long blockHeight)
    {
        throw new NotImplementedException();
    }

    public Task<long> GetConsumedHeightAsync(string chainId)
    {
        throw new NotImplementedException();
    }
}