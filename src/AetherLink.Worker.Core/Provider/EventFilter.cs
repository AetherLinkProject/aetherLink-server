using System.Threading.Tasks;
using AetherLink.Indexer.Dtos;
using AetherLink.Worker.Core.Constants;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IEventFilter
{
    string EventName { get; }
    Task ProcessAsync(TransactionEventDto logEvent);
}

public abstract class EventFilter : IEventFilter
{
    public abstract string EventName { get; }

    protected EventFilter()
    {
    }

    public abstract Task ProcessAsync(TransactionEventDto logEvent);
}

public class AutomationFilter : EventFilter, ISingletonDependency
{
    public override string EventName => EventFilterConstants.Automation;
    private readonly IFilterStorage _filterStorage;

    public override async Task ProcessAsync(TransactionEventDto logEvent)
        => await _filterStorage.ProcessEventAsync(logEvent);

    public AutomationFilter(IFilterStorage filterStorage)
    {
        _filterStorage = filterStorage;
    }
}