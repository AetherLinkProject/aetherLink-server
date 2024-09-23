using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Automation.Args;
using AetherLink.Worker.Core.Automation.Providers;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Common.ContractHandler;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUglify.Helpers;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IFilterStorage
{
    Task ProcessEventAsync(TransactionEventDto logEvent);
    Task AddFilterAsync(LogUpkeepInfoDto logUpkeepInfoInfo);
    Task DeleteFilterAsync(LogUpkeepInfoDto logUpkeepInfo);
}

public class FilterStorage : IFilterStorage, ISingletonDependency
{
    private readonly ContractOptions _chainOptions;
    private readonly ILogger<FilterStorage> _logger;
    private readonly object _filterMemoryLock = new();
    private readonly IStorageProvider _storageProvider;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IOracleContractProvider _oracleContractProvider;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, List<string>>> _eventFilters = new();

    public FilterStorage(ILogger<FilterStorage> logger, IOptions<ContractOptions> chainInfoOptions,
        IOracleContractProvider oracleContractProvider, IBackgroundJobManager backgroundJobManager,
        IStorageProvider storageProvider)
    {
        _logger = logger;
        _storageProvider = storageProvider;
        _chainOptions = chainInfoOptions.Value;
        _backgroundJobManager = backgroundJobManager;
        _oracleContractProvider = oracleContractProvider;
        Initialize().GetAwaiter().GetResult();
    }

    public async Task ProcessEventAsync(TransactionEventDto logEvent)
    {
        var chainId = logEvent.ChainId;
        var eventName = logEvent.EventName;
        var contractAddress = logEvent.ContractAddress;
        var blockHeight = logEvent.BlockHeight;

        _logger.LogInformation("[FilterStorage] {event} found at {chain} {height}", eventName, chainId, blockHeight);

        if (!_eventFilters.TryGetValue(chainId, out _) || !_eventFilters[chainId]
                .TryGetValue(GenerateEventId(contractAddress, eventName), out var filters) ||
            filters.Count == 0)
        {
            _logger.LogDebug("[FilterStorage] There no upkeep for {event}", eventName);
            return;
        }

        var eventKey =
            AutomationHelper.GenerateTransactionEventKey(chainId, contractAddress, eventName, blockHeight,
                logEvent.Index);
        await _storageProvider.SetAsync(eventKey, logEvent, TimeSpan.FromDays(1));

        var epoch = await _oracleContractProvider.GetStartEpochAsync(logEvent.ChainId, logEvent.BlockHeight);
        var logUpkeepInfos = await _storageProvider.GetAsync<LogUpkeepInfoDto>(filters);
        foreach (var kp in logUpkeepInfos)
        {
            var logUpkeepKey = kp.Key;
            var logUpkeepInfo = kp.Value;
            await _backgroundJobManager.EnqueueAsync(
                new AutomationLogTriggerArgs
                {
                    Context = new()
                    {
                        RequestId = logUpkeepInfo.UpkeepId,
                        ChainId = logUpkeepInfo.ChainId,
                        Epoch = epoch
                    },
                    StartTime = logEvent.StartTime,
                    TransactionEventStorageId = eventKey,
                    LogUpkeepStorageId = logUpkeepKey
                });
        }
    }

    public async Task AddFilterAsync(LogUpkeepInfoDto logUpkeepInfo)
    {
        AddFilter(logUpkeepInfo);

        await UpdateChainFilterStorageAsync(logUpkeepInfo.ChainId);
    }

    public async Task DeleteFilterAsync(LogUpkeepInfoDto logUpkeepInfo)
    {
        DeleteFilter(logUpkeepInfo);

        await UpdateChainFilterStorageAsync(logUpkeepInfo.ChainId);
    }

    private void AddFilter(LogUpkeepInfoDto logUpkeepInfo)
    {
        lock (_filterMemoryLock)
        {
            var chainId = logUpkeepInfo.ChainId;
            var eventId = GenerateEventId(logUpkeepInfo.TriggerContractAddress, logUpkeepInfo.TriggerEventName);
            var upkeepStorageId = IdGeneratorHelper.GenerateUpkeepInfoId(chainId, logUpkeepInfo.UpkeepId);
            if (!_eventFilters[chainId].TryGetValue(eventId, out var eventFilters))
            {
                _eventFilters[chainId][eventId] = new() { upkeepStorageId };
                _logger.LogDebug("[FilterStorage] {chain} filter init {eventId}.", chainId, eventId);
                return;
            }

            eventFilters.Add(upkeepStorageId);
            _eventFilters[chainId][eventId] = eventFilters.Distinct().ToList();
        }
    }

    private void DeleteFilter(LogUpkeepInfoDto logUpkeepInfo)
    {
        lock (_filterMemoryLock)
        {
            var chainId = logUpkeepInfo.ChainId;
            var upkeepId = logUpkeepInfo.UpkeepId;
            var eventId = GenerateEventId(logUpkeepInfo.TriggerContractAddress, logUpkeepInfo.TriggerEventName);
            if (!_eventFilters[chainId].TryGetValue(eventId, out var eventFilters))
            {
                _logger.LogWarning("[FilterStorage] {chain} {upkeep} no need remove.", chainId, upkeepId);
                return;
            }

            eventFilters.Remove(IdGeneratorHelper.GenerateUpkeepInfoId(chainId, upkeepId));
            _eventFilters[chainId][eventId] = eventFilters;
        }
    }

    private async Task UpdateChainFilterStorageAsync(string chainId)
    {
        var chainFilters = new Dictionary<string, List<string>>();
        _eventFilters[chainId].ToList().ForEach(kvp => chainFilters[kvp.Key] = kvp.Value);

        _logger.LogDebug($"[FilterStorage] Update {chainId} filters");
        await _storageProvider.SetAsync<EventFiltersStorageDto>(GenerateFiltersStorageId(chainId),
            new() { Filters = chainFilters });
    }

    private string GenerateEventId(string address, string eventName)
        => IdGeneratorHelper.GenerateId(address, eventName);

    private string GenerateFiltersStorageId(string chainId)
        => IdGeneratorHelper.GenerateId(RedisKeyConstants.EventFiltersKey, chainId);

    private async Task Initialize()
    {
        foreach (var chainId in _chainOptions.ChainInfos.Keys)
        {
            _eventFilters[chainId] = new();
            var result = await _storageProvider.GetAsync<EventFiltersStorageDto>(GenerateFiltersStorageId(chainId));
            if (result == null || !result.Filters.Any())
            {
                _logger.LogDebug("[FilterStorage] There is no filter in storage on {chain} yet.", chainId);
            }
            else
            {
                _logger.LogInformation("[FilterStorage] Init {count} Filters on {chain}", result.Filters.Count,
                    chainId);
                result.Filters.ForEach(f => _eventFilters[chainId][f.Key] = f.Value);
            }

            _logger.LogInformation("[FilterStorage] Finished FiltersStorage initialization.");
        }
    }
}