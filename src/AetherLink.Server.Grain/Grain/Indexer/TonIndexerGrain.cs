using AetherLink.Indexer.Dtos;
using AetherLink.Indexer.Provider;
using AetherLink.Server.Grains.State;
using Microsoft.Extensions.Logging;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Server.Grains.Grain.Indexer;

public interface ITonIndexerGrain : IGrainWithStringKey
{
    Task<GrainResultDto<List<TonTransactionGrainDto>>> SearchTonTransactionsAsync();
}

public class TonIndexerGrain : Grain<TonIndexerState>, ITonIndexerGrain
{
    private readonly IObjectMapper _mapper;
    private readonly ITonIndexerProvider _indexer;
    private readonly ILogger<TonIndexerGrain> _logger;

    public TonIndexerGrain(ILogger<TonIndexerGrain> logger, ITonIndexerProvider indexer, IObjectMapper mapper)
    {
        _mapper = mapper;
        _logger = logger;
        _indexer = indexer;
    }

    public async Task<GrainResultDto<List<TonTransactionGrainDto>>> SearchTonTransactionsAsync()
    {
        // var transactions = await _indexer.SubscribeTransactionAsync(State.LatestTransactionLt);
        var transactions = await _indexer.SubscribeTransactionAsync(28227653000001.ToString());

        _logger.LogDebug($"[TonIndexerGrain] Get total {transactions.Count} Ton transaction");

        if (transactions.Count == 0) return new() { Message = "Empty data", Data = new() };

        await WriteStateAsync();
        
        return new() { Data = _mapper.Map<List<TonTransactionDto>, List<TonTransactionGrainDto>>(transactions) };
    }
}