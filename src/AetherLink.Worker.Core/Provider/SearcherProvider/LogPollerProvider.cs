using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AetherLink.Indexer.Dtos;
using AetherLink.Indexer.Provider;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider.SearcherProvider;

public interface ILogPollerProvider
{
    Task<LookBackBlockHeightDto> GetLookBackBlockHeightAsync(string chainId);
    Task SetLookBackBlockHeightAsync(string chainId, long blockHeight);
    Task<List<TransactionEventDto>> PollLogEventsAsync(string chainId, long to, long from);
    Task HandlerEventAsync(TransactionEventDto logEvent);
}

public class LogPollerProvider : ILogPollerProvider, ISingletonDependency
{
    private readonly IStorageProvider _storageProvider;
    private readonly IAeFinderProvider _aeFinderProvider;
    private readonly Dictionary<string, IEventFilter> _filter;

    public LogPollerProvider(IStorageProvider storageProvider, AeFinderProvider aeFinderProvider,
        IEnumerable<IEventFilter> filters)
    {
        _storageProvider = storageProvider;
        _aeFinderProvider = aeFinderProvider;
        _filter = filters.ToDictionary(x => x.EventName, y => y);
    }

    public async Task<LookBackBlockHeightDto> GetLookBackBlockHeightAsync(string chainId)
        => await _storageProvider.GetAsync<LookBackBlockHeightDto>(GenerateStorageKey(chainId));

    public async Task SetLookBackBlockHeightAsync(string chainId, long blockHeight)
        => await _storageProvider.SetAsync(GenerateStorageKey(chainId),
            new LookBackBlockHeightDto { BlockHeight = blockHeight });

    public async Task<List<TransactionEventDto>> PollLogEventsAsync(string chainId, long to, long from)
        => await _aeFinderProvider.GetTransactionLogEventsAsync(chainId, to, from);

    public async Task HandlerEventAsync(TransactionEventDto logEvent)
    {
        if (_filter.TryGetValue(logEvent.EventName, out var request))
        {
            await request.ProcessAsync(logEvent);
        }

        await _filter[EventFilterConstants.Automation].ProcessAsync(logEvent);
    }

    private string GenerateStorageKey(string chainId) =>
        IdGeneratorHelper.GenerateId(RedisKeyConstants.LookBackBlocksKey, chainId);
}