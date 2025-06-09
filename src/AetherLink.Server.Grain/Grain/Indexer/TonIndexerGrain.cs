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
        if (string.IsNullOrEmpty(State.Id)) State.Id = this.GetPrimaryKeyString();

        var currentTransactionLt = State.LatestTransactionLt;
        var transactions = await _indexer.SubscribeTransactionAsync(currentTransactionLt);

        _logger.LogDebug($"[TonIndexerGrain] Get total {transactions.Count} Ton transaction");

        if (!transactions.Any() ||
            (transactions.Count == 1 && transactions[0].Lt.ToString() == State.LatestTransactionLt))
        {
            _logger.LogDebug($"[TonIndexerGrain] Don't get new Ton transaction");
            return new() { Message = "Empty data", Data = new() };
        }

        var latestTransactionLt = transactions.Last().Lt.ToString();
        State.LatestTransactionLt = latestTransactionLt;

        _logger.LogInformation($"[TonIndexerGrain] Update LatestTransactionLt to {latestTransactionLt}");

        await WriteStateAsync();

        return new() { Data = _mapper.Map<List<TonTransactionDto>, List<TonTransactionGrainDto>>(transactions) };
    }
}