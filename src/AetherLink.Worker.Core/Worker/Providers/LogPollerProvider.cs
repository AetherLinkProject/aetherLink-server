using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Worker.Providers;

public interface ILogPollerProvider
{
    Task<LookBackBlockHeightDto> GetLookBackBlockHeightAsync(string chainId);
    Task SetLookBackBlockHeightAsync(string chainId, long blockHeight);
    Task<long> GetConfirmedBlockHeightAsync(string chainId);
    Task<List<TransactionEventDto>> PollLogEventsAsync(string chainId, long to, long from);
    Task HandlerEventAsync(TransactionEventDto logEvent);
}

public class LogPollerProvider : ILogPollerProvider, ISingletonDependency
{
    private readonly IStorageProvider _storageProvider;
    private readonly IIndexerProvider _indexerProvider;
    private readonly ILogger<LogPollerProvider> _logger;
    private readonly Dictionary<string, IEventFilter> _filter;

    public LogPollerProvider(IStorageProvider storageProvider, IIndexerProvider indexerProvider,
        IEnumerable<IEventFilter> filters, ILogger<LogPollerProvider> logger)
    {
        _logger = logger;
        _storageProvider = storageProvider;
        _indexerProvider = indexerProvider;
        _filter = filters.ToDictionary(x => x.EventName, y => y);
    }

    public async Task<LookBackBlockHeightDto> GetLookBackBlockHeightAsync(string chainId)
        => await _storageProvider.GetAsync<LookBackBlockHeightDto>(GenerateStorageKey(chainId));

    public async Task SetLookBackBlockHeightAsync(string chainId, long blockHeight)
        => await _storageProvider.SetAsync(GenerateStorageKey(chainId),
            new LookBackBlockHeightDto { BlockHeight = blockHeight });

    public async Task<long> GetConfirmedBlockHeightAsync(string chainId)
        => await _indexerProvider.GetIndexBlockHeightAsync(chainId);

    public async Task<List<TransactionEventDto>> PollLogEventsAsync(string chainId, long to, long from)
        => await _indexerProvider.GetTransactionLogEventsAsync(chainId, to, from);

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